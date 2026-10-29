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
    //internal readonly EntityUid Uid;
    //internal readonly int Pending; /* 0 = real UID, else index of spawn cmd + 1 */
    //internal readonly CommandBuffer? Owner;
    internal sealed class Slot { public EntityUid Uid; public bool Spawned; }

    private readonly EntityUid _uid;
    private readonly Slot? _slot;

    private EntityRef(EntityUid uid, Slot? slot) { _uid = uid; _slot = slot; }

    public static implicit operator EntityRef(EntityUid uid) => new(uid, null);
    internal static EntityRef FromPending(Slot slot) => new(default, slot);

    /// <summary>
    /// True if waiting to be spawned.
    /// </summary>
    public bool IsPending => _slot is { Spawned: false };

    public bool TryGetUid(out EntityUid uid)
    {
        if (_slot == null) { uid = _uid; return true; }
        uid = _slot.Spawned ? _slot.Uid : default;
        return _slot.Spawned;
    }

    internal EntityUid ResolveOrDefault() => _slot == null ? _uid : (_slot.Spawned ? _slot.Uid : default);
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
        public Action<EntityUid>? Callback; /* exclusively spawns */
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

    public int Count => _commands.Count;

    /// <summary>
    /// Creates an entity from a prototype ID.
    /// The returned references can be used by later add/remove/etc commands in this very buffer, or even others, even if the Spawn command isn't immediately executed.
    /// After the spawn has happened, the <see cref="EntityUid"/> is available through <see cref="EntityRef.TryGetUid"/>.
    /// </summary>
    public EntityRef Spawn(string prototypeId) => Spawn(prototypeId, null);

    public EntityRef Spawn(string prototypeId, Action<EntityUid>? onSpawned)
    {
        var slot = new EntityRef.Slot();
        _commands.Add(new Command { Kind = Kind.Spawn, Prototype = prototypeId, Payload = slot, Callback = onSpawned});
        return EntityRef.FromPending(slot);
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
        => _commands.Add(new Command { Kind = kind, Target = entity, Payload = payload });

    public void Clear() => _commands.Clear();

    /// <summary>
    /// Moves everything pending from another command buffer into this one, then empties the other.
    /// </summary>
    /// <param name="other"></param>
    internal void AppendFrom(CommandBuffer other)
    {
        _commands.AddRange(other._commands);
        other.Clear();
    }

    internal void Apply(World world)
    {
        if (_commands.Count == 0) return;

        var batch = _commands;
        _commands = _spare ?? new List<Command>();
        _spare = null;

        List<Exception>? failures = null;

        try
        {
            foreach (var command in CollectionsMarshal.AsSpan(batch))
            {
                try
                {
                    if (command.Kind == Kind.Spawn)
                    {
                        var slot = (EntityRef.Slot)command.Payload!;
                        slot.Uid = world.Spawn(command.Prototype!);
                        slot.Spawned = true;
                        command.Callback?.Invoke(slot.Uid);
                        continue;
                    }

                    var target = command.Target.ResolveOrDefault();
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
