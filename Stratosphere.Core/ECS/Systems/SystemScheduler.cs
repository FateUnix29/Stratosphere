using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Stratosphere.Core.ECS.Systems;

/// <summary>
/// Notes down what a parallel system can touch. Lives in a thread-static for as long as said system runs.
/// </summary>
internal sealed class AccessScope
{
    [ThreadStatic] public static AccessScope? Current;

    public readonly string SystemName;
    private readonly HashSet<Type> _allowed;

    public AccessScope(string systemName, HashSet<Type> allowed) { SystemName = systemName; _allowed = allowed; }
    public bool Allows(Type component) => _allowed.Contains(component);
}


internal sealed class SystemScheduler
{
    internal sealed class Node
    {
        public required int Index { get; init; }
        public required EntitySystem System { get; init; }
        public required bool Exclusive { get; init; }
        public required HashSet<Type> Reads { get; init; }
        public required HashSet<Type> Writes { get; init; }
        public required AccessScope Scope { get; init; }

        public List<int> DependsOn { get; } = new();
        public List<int> Dependents { get; } = new();
    }

    private readonly Node[] _nodes;

    public bool AllExclusive { get; }

    public IReadOnlyList<Node> Nodes => _nodes;

    private SystemScheduler(Node[] nodes)
    {
        _nodes = nodes;
        AllExclusive = nodes.All(n => n.Exclusive);
    }

    public static SystemScheduler Build(IReadOnlyList<EntitySystem> systems)
    {
        var nodes = new Node[systems.Count];

        for (int i = 0; i < nodes.Length; i++)
        {
            var type = systems[i].GetType();
            var attribute = type.GetCustomAttribute<ParallelSystemAttribute>(inherit: false);
            var reads = new HashSet<Type>(attribute?.Reads ?? Array.Empty<Type>());
            var writes = new HashSet<Type>(attribute?.Writes ?? Array.Empty<Type>());
            reads.UnionWith(writes);

            nodes[i] = new Node
            {
                Index = i,
                System = systems[i],
                Exclusive = attribute == null,
                Reads = reads,
                Writes = writes,
                Scope = new AccessScope(type.Name, reads)
            };
            systems[i].IsParallel = attribute != null;
        }

        for (int later = 0; later < nodes.Length; later++)
            for (int earlier = 0; earlier < later; earlier++)
                if (MustWait(nodes[later], nodes[earlier]))
                {
                    nodes[later].DependsOn.Add(earlier);
                    nodes[earlier].Dependents.Add(later);
                }

        return new SystemScheduler(nodes);
    }

    private static bool MustWait(Node later, Node earlier)
    {
        if (later.Exclusive || earlier.Exclusive) return true;

        if (later.Writes.Overlaps(earlier.Reads) || earlier.Writes.Overlaps(later.Reads)) return true;

        var laterType = later.System.GetType();
        var earlierType = earlier.System.GetType();

        return laterType.GetCustomAttributes<UpdatesAfterAttribute>(false).Any(a => a.Systems.Contains(earlierType))
        || earlierType.GetCustomAttributes<UpdatesBeforeAttribute>(false).Any(a => a.Systems.Contains(laterType));
    }

    public void Run(World world, float deltaTime, int maxParallelism)
    {
        if (AllExclusive || maxParallelism <= 1 || _nodes.Length < 2)
            RunSerial(world, deltaTime);
        else
            RunParallel(world, deltaTime);
    }

    private void RunSerial(World world, float deltaTime)
    {
        foreach (var node in _nodes)
        {
            if (node.Exclusive) world.FlushCommands();
            RunNode(world, node, deltaTime);
        }
    }

    private static void RunNode(World world, Node node, float deltaTime)
    {
        if (node.Exclusive)
        {
            world.RunSystemUpdate(node.System, deltaTime);
            return;
        }

        world.EnterParallel();
        AccessScope.Current = node.Scope;
        try { world.RunSystemUpdate(node.System, deltaTime); }
        finally
        {
            AccessScope.Current = null;
            world.ExitParallel();
        }
    }

    private sealed class Run_
    {
        public readonly World World;
        public readonly float DeltaTime;
        public readonly int[] Pending;
        public int Remaining;
        public volatile bool Failed;
        public readonly ConcurrentQueue<Node> MainQueue = new();
        public readonly SemaphoreSlim MainSignal = new(0);
        public readonly Exception?[] Errors;

        public Run_(World world, float deltaTime, int count)
        {
            World = world; DeltaTime = deltaTime;
            Pending = new int[count];
            Errors = new Exception?[count];
            Remaining = count;
        }
    }

    private void RunParallel(World world, float deltaTime)
    {
        var run = new Run_(world, deltaTime, _nodes.Length);
        foreach (var node in _nodes) run.Pending[node.Index] = node.DependsOn.Count;

        foreach (var node in _nodes)
            if (node.DependsOn.Count == 0) Ready(run, node);

        while (true)
        {
            run.MainSignal.Wait();

            if (run.MainQueue.TryDequeue(out var node))
            {
                if (!run.Failed) world.FlushCommands();
                Execute(run, node);
            }
            else if (Volatile.Read(ref run.Remaining) == 0) break;
        }

        var first = run.Errors.FirstOrDefault(e => e != null);
        if (first != null)
        {
            world.DiscardCommands();
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private void Ready(Run_ run, Node node)
    {
        if (node.Exclusive)
        {
            run.MainQueue.Enqueue(node);
            run.MainSignal.Release();
        }
        else
        {
            ThreadPool.UnsafeQueueUserWorkItem(static state => state.Scheduler.Execute(state.Run, state.Node), (Scheduler: this, Run: run, Node: node), preferLocal: false);
        }
    }

    private void Execute(Run_ run, Node node)
    {
        if (!run.Failed)
        {
            try { RunNode(run.World, node, run.DeltaTime); }
            catch (Exception e)
            {
                run.Errors[node.Index] = e;
                run.Failed = true; /* prevents anything new from starting, though things already running pack up. */
            }
        }

        foreach (int dependent in node.Dependents)
            if (Interlocked.Decrement(ref run.Pending[dependent]) == 0)
                Ready(run, _nodes[dependent]);

        if (Interlocked.Decrement(ref run.Remaining) == 0)
            run.MainSignal.Release();
    }
}
