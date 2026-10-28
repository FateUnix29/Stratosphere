using System.Runtime.InteropServices;
using System.Text;
using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS;

/// <summary>
/// An entity named in a command... One that exists already, or one that a spawn call is going to make.
/// Has implicit conversion from <see cref="EntityUid"/>.
/// </summary>
public readonly struct EntityRef
{
    internal readonly EntityUid Uid;
    internal readonly int Pending; /* 0 = real UID, else index of spawn cmd + 1 */
    internal readonly CommandBuffer? Owner;

    private EntityRef(EntityUid uid, int pending, CommandBuffer? owner) { Uid = uid; Pending = pending; Owner = owner; }

    public static implicit operator EntityRef(EntityUid uid) => new(uid, 0, null);
    internal static EntityRef FromPending(int index, CommandBuffer owner) => new(default, index+1, owner);

    public bool IsPending => Pending != 0;
}

/// <summary>
/// A list of changes to the world's structure, such as entities and components spawning and being removed, that happen later.
/// This is required due to multithreading.
/// </summary>
/// <remarks>
/// </remarks>
public sealed class CommandBuffer
{
    private enum Kind : byte { Spawn, Add, Remove, Destroy, Raise }
    private struct Command
    {
        public Kind Kind;
        public EntityRef Target;
        public string? Prototype;
        public object? Payload;
    }

    private abstract class DeferredEvent { public abstract void Raise(World world, EntityUid entity); }
    private sealed class DeferredEvent<TEvent> : DeferredEvent where TEvent : struct, IEvent
    {
        private TEvent _args;
        public DeferredEvent(TEvent args) { _args = args; }
        public override void Raise(World world, EntityUid entity) => world.RaiseLocalEvent(entity, ref _args);
    }

    private List<Command> _commands = new();
    private List<Command>? _spare;
    private int _pendingCount;

    public int Count => _commands.Count;

    /// <summary>
    /// Creates an entity from a prototype ID.
    /// The returned references can be used by later add/remove/etc commands in this very buffer, even if the Spawn command isn't immediately executed.
    /// </summary>
    public EntityRef Spawn(string prototypeId)
    {
        var reference = EntityRef.FromPending(_pendingCount++, this); /* always good to remember... _pendingCount's original value is used and THEN it inc's. */
        _commands.Add(new Command { Kind = Kind.Spawn, Prototype = prototypeId });
        return reference;
    }

    public void Add<T>(EntityRef entity, T component) where T : class, IComponent
        => Queue(Kind.Add, entity, payload: component);

    public void Remove<T>(EntityRef entity) where T : class, IComponent
        => Queue(Kind.Remove, entity, payload: typeof(T));

    public void Destroy(EntityRef entity) => Queue(Kind.Destroy, entity);

    /// <summary>
    /// Raises an event on an entity sometime later on the main thread, where handlers can do what they please.
    /// </summary>
    public void Raise<TEvent>(EntityRef entity, TEvent args) where TEvent : struct, IEvent
        => Queue(Kind.Raise, entity, payload: new DeferredEvent<TEvent>(args));


    private void Queue(Kind kind, EntityRef entity, object? payload = null)
    {
        if (entity.IsPending && entity.Owner != this)
            throw new InvalidOperationException("Entity reference came from another command buffer. Referencing a spawned entity only works in the same buffer that spawned it.");

        _commands.Add(new Command { Kind = kind, Target = entity, Payload = payload });
    }

    public void Clear()
    {
        _commands.Clear();
        _pendingCount = 0;
    }

    /// <summary>
    /// Moves everything pending from another command buffer into this one, then empties the other.
    /// </summary>
    /// <param name="other"></param>
    internal void AppendFrom(CommandBuffer other)
    {
        int offset = _pendingCount;
        foreach (var command in CollectionsMarshal.AsSpan(other._commands))
        {
            var copy = command;
            if (copy.Target.IsPending)
                copy.Target = EntityRef.FromPending(copy.Target.Pending - 1 + offset, this);
            _commands.Add(copy);
        }
        _pendingCount += other._pendingCount;
        other.Clear();
    }

    internal void Apply(World world)
    {
        if (_commands.Count == 0) return;

        var batch = _commands;
        _commands = _spare ?? new List<Command>();
        _spare = null;

        var spawned = _pendingCount > 0 ? new EntityUid[_pendingCount] : null;
        _pendingCount = 0;

        List<Exception>? failures = null;
        int nextSpawn = 0;

        try
        {
            foreach (var command in CollectionsMarshal.AsSpan(batch))
            {
                try
                {
                    if (command.Kind == Kind.Spawn)
                    {
                        spawned![nextSpawn] = world.Spawn(command.Prototype!);
                        nextSpawn++;
                        continue;
                    }

                    var target = command.Target.IsPending ? spawned![command.Target.Pending - 1] : command.Target.Uid;
                    if (!target.IsValid() || !world.Exists(target)) continue;

                    switch (command.Kind)
                    {
                        case Kind.Add: world.AddBoxed(target, (IComponent)command.Payload!); break;
                        case Kind.Remove: world.RemoveBoxed(target, (Type)command.Payload!); break;
                        case Kind.Destroy: world.Destroy(target); break;
                        case Kind.Raise: ((DeferredEvent)command.Payload!).Raise(world, target); break;
                    }
                }
                catch (Exception e)
                {
                    (failures ??= new()).Add(e);
                    if (command.Kind == Kind.Spawn) nextSpawn++;
                }
            }
        }
        finally
        {
            batch.Clear();
            _spare = batch;
        }

        if (failures != null)
            throw failures.Count == 1 ? failures[0] : new AggregateException("Several commands failed.", failures);
    }
}
