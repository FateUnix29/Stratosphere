using System.Runtime.ExceptionServices;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Core.ECS;

public sealed partial class World
{
    private int _parallelDepth;
    private SystemScheduler? _scheduler;
    private readonly Stack<CommandBuffer> _bufferPool = new();

    /// <summary>
    /// The most threads that'll be doing the World's work at the same time.
    /// 1 turns threading off, and while that's slower, it should produce identical results. Good for debugging.
    /// </summary>
    public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// Decides whether or not to throw when a parallel system touches something it didn't ask to. Costs a tiny bit every component access.
    /// </summary>
    public bool CheckSystemAccess { get; set; } =
#if DEBUG
        true;
#else
        false;
#endif

    public bool InParallelSection => Volatile.Read(ref _parallelDepth) != 0;

    internal void EnterParallel() => Interlocked.Increment(ref _parallelDepth);
    internal void ExitParallel() => Interlocked.Decrement(ref _parallelDepth);

    private void ForbidInParallel(string operation)
    {
        if (Volatile.Read(ref _parallelDepth) != 0)
            throw new InvalidOperationException(
                $"{operation} cannot be directly carried out while systems run in parallel. Chances are, this is due to other threads being able to read the very things it'd change. "
                + "Instead, queue it so that it happens once the parallel work is over."
            );
    }

    private void CheckAccess(Type component)
    {
        if (!CheckSystemAccess) return;

        var scope = AccessScope.Current;
        if (scope != null && !scope.Allows(component))
            throw new InvalidOperationException(
                $"{scope.SystemName} is a parallel system trying to read/write {component.Name}, which it did not declare."
                + $"Add it to [ParallelSystem(Reads = ..., Writes = ...)], please. That way, the world's scheduler knows what it can run alongside."
            );
    }

    internal void FlushCommands()
    {
        for (int round = 0; ; round++)
        {
            bool any = false;
            for (int i = 0; i < _systems.Count; i++)
            {
                var buffer = _systems[i].CommandsBuffer;
                if (buffer.Count == 0) continue;

                any = true;
                buffer.Apply(this);
            }

            if (!any) return;
            if (round >= 128) throw new InvalidOperationException("Commands keep queueing more and more commands, 128 deep... Something is almost certainly looping.");
        }
    }

    internal void DiscardCommands() { foreach (var system in _systems) system.CommandsBuffer.Clear(); }

    internal IReadOnlyList<EntitySystem> Systems => _systems;

    private SystemScheduler Scheduler => _scheduler ??= SystemScheduler.Build(_systems);

    private CommandBuffer RentBuffer()
    { lock (_bufferPool) return _bufferPool.Count > 0 ? _bufferPool.Pop() : new CommandBuffer(); }

    private void ReturnBuffer(CommandBuffer buffer)
    { buffer.Clear(); lock (_bufferPool) _bufferPool.Push(buffer); }

    private void RunChunked(int count, int chunkSize, CommandBuffer? into, Action<int, int, CommandBuffer?> body)
    {
        if (count == 0) return;
        if (chunkSize < 1) chunkSize = 1;

        int chunks = (count + chunkSize - 1) / chunkSize;
        CommandBuffer[]? buffers = null;

        if (into != null)
        {
            buffers = new CommandBuffer[chunks];
            for (int c = 0; c < chunks; c++) buffers[c] = RentBuffer();
        }

        var scope = AccessScope.Current;

        void Chunk(int c)
        {
            int start = c * chunkSize;
            int end = Math.Min(count, start+chunkSize);

            var previous = AccessScope.Current;
            AccessScope.Current = scope;
            try { body(start, end, buffers?[c]); }
            finally { AccessScope.Current = previous; }
        }
    
        EnterParallel();
        try
        {
            if (MaxParallelism > 1 && chunks > 1)
            {
                try { Parallel.For(0, chunks, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism }, Chunk); }
                catch (AggregateException e) when (e.InnerExceptions.Count > 0)
                { ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw(); }
            }
            else
            {
                for (int c = 0; c < chunks; c++) Chunk(c);
            }
        }
        finally { ExitParallel(); }

        if (buffers != null)
        {
            foreach (var buffer in buffers)
            {
                into!.AppendFrom(buffer);
                ReturnBuffer(buffer);
            }
        }
    }

    internal void ParallelForEach<T>(Action<EntityUid, T> body, int chunkSize) where T : class, IComponent
        => ParallelForEachCore<T>(null, chunkSize, (store, start, end, _) =>
        {
            for (int i = start; i < end; i++) body(store.OwnerAt(i), store.ComponentAt(i));
        });

    internal void ParallelForEach<T>(Action<EntityUid, T, CommandBuffer> body, CommandBuffer into, int chunkSize) where T : class, IComponent
        => ParallelForEachCore<T>(into, chunkSize, (store, start, end, commands) =>
        {
            for (int i = start; i < end; i++) body(store.OwnerAt(i), store.ComponentAt(i), commands!);
        });

    private void ParallelForEachCore<T>(CommandBuffer? into, int chunkSize, Action<ComponentStore<T>, int, int, CommandBuffer?> chunk) where T : class, IComponent
    {
        CheckAccess(typeof(T));
        if (!_stores.TryGetValue(typeof(T), out var found)) return;

        var store = (ComponentStore<T>) found;
        int version = store.Version;

        RunChunked(store.Count, chunkSize, into, (start, end, commands) => chunk(store, start, end, commands));

        if (store.Version != version) throw QueryModified<T>();
    }

    internal void ParallelForEach<T1, T2>(Action<EntityUid, T1, T2> body, int chunkSize)
        where T1 : class, IComponent where T2 : class, IComponent
        => ParallelForEachCore<T1, T2>(null, chunkSize, (entity, a, b, _) => body(entity, a, b));

    internal void ParallelForEach<T1, T2>(Action<EntityUid, T1, T2, CommandBuffer> body, CommandBuffer into, int chunkSize)
        where T1 : class, IComponent where T2 : class, IComponent
        => ParallelForEachCore<T1, T2>(into, chunkSize, (entity, a, b, commands) => body(entity, a, b, commands!));

    private void ParallelForEachCore<T1, T2>(CommandBuffer? into, int chunkSize, Action<EntityUid, T1, T2, CommandBuffer?> each)
        where T1 : class, IComponent where T2 : class, IComponent
    {
        CheckAccess(typeof(T1));
        CheckAccess(typeof(T2));
        if (!_stores.TryGetValue(typeof(T1), out var found1) || !_stores.TryGetValue(typeof(T2), out var found2)) return;

        var first = (ComponentStore<T1>) found1;
        var second = (ComponentStore<T2>) found2;
        int version1 = first.Version, version2 = second.Version;

        /* Walk whoever's shorter, look up the other one. Looking up is merely a read... Thread-safe. */
        if (first.Count <= second.Count)
            RunChunked(first.Count, chunkSize, into, (start, end, commands) =>
            {
                for (int i = start; i < end; i++)
                    if (second.TryGet(first.OwnerAt(i), out var b)) each(first.OwnerAt(i), first.ComponentAt(i), b, commands);
            });
        else
            RunChunked(second.Count, chunkSize, into, (start, end, commands) =>
            {
                for (int i = start; i < end; i++)
                    if (first.TryGet(second.OwnerAt(i), out var a)) each(second.OwnerAt(i), a, second.ComponentAt(i), commands);
            });

        if (first.Version != version1 || second.Version != version2) throw QueryModified<T1>();
    }
}
