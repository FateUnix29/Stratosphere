using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.ECS.Systems;
using Stratosphere.Core.Logging;

namespace Stratosphere.Core.ECS;

public sealed class World
{
    private readonly PrototypeManager _prototypes;
    private readonly HashSet<EntityUid> _alive = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();
    private readonly List<EntityUid> _queuedForDestruction = new();

	/* Ran in order of addition */
	private readonly List<EntitySystem> _systems = new();
    private readonly Dictionary<Type, EntitySystem> _systemsByType = new();
    private int _initializedSystems;

    private readonly EventBus _events;

    private long _nextId = 1;
    public GameTick CurTick { get; private set; } = GameTick.Zero;

    public int EntityCount => _alive.Count;

    public LogManager Logs { get; }
    private readonly Sawmill _log;

    public World(PrototypeManager prototypes, LogManager? logs = null)
    {
    	_prototypes = prototypes;
    	_events = new EventBus(this);
        Logs = logs ?? new LogManager();
        _log = Logs.GetSawmill("world");

        /* The engine's own systems (for now, just the transforms system) are always present, same as the engine's own components! */
        AddSystemsFromAssembly(typeof(World).Assembly);
	}

    /// <summary>
    /// Advances world by a single tick. Systems update, destruction happens, etc.
    /// </summary>
    public void Tick(float deltaTime)
    {
        InitializeSystems();

 		int count = _systems.Count; /* Preventing us from getting fucked over if a system is somehow added mid-tick. */
 		for (int i = 0; i < count; i++) UpdateSystem(_systems[i], deltaTime);

		FlushQueuedDestructions();

        CurTick += 1;
    }

	/// <summary>Advancss world by one tick in which no time passes. Useful for turn-based games and the likes. */
	public void Tick() => Tick(0f);

    private void UpdateSystem(EntitySystem system, float deltaTime)
    {
        try { system.Update(deltaTime); }
        catch (Exception e)
        {
            _log.Error(e, $"{system.GetType().Name} threw during tick {CurTick}!");
            throw;
        }
    }

	private void FlushQueuedDestructions()
	{
		if (_queuedForDestruction.Count == 0) return;

		/* Copy the list and clear it rather than just iterating jt directly, in case a destruction queues more destructions. */
		var batch = _queuedForDestruction.ToArray();
		_queuedForDestruction.Clear();

		foreach (EntityUid queued in batch) Destroy(queued);
	}
	

	public T AddSystem<T>(T system) where T : EntitySystem
	{
		if (!_systemsByType.TryAdd(system.GetType(), system))
			throw new InvalidOperationException($"A {system.GetType().Name} has already been added.");

		system.Attach(this);
		_systems.Add(system);
        _log.Debug($"Added system {system.GetType().Name} (#{_systems.Count}).");
		return system;
	}

	public T GetSystem<T>() where T : EntitySystem
		=> _systemsByType.TryGetValue(typeof(T), out var system)
            ? (T) system
            : throw new InvalidOperationException($"No {typeof(T).Name} has been added.");

    /// <summary>
    /// Creates and adds every system in the given assembly marked with the RegisterSystem attribute,
    /// in an order that respects their UpdatesAfter attributes UpdatesBefore attributes.
    /// Systems with no specified ordering between them are simply sorted by name, in order to keep the order the same every run.
    /// Manually added systems remain unaffected by this.
    /// </summary>
    public void AddSystemsFromAssembly(Assembly assembly)
    {
        foreach (var type in SystemScanner.FindInOrder(assembly, _systemsByType.Keys))
            AddSystem((EntitySystem)Activator.CreateInstance(type)!);
    }

    /// <summary>
    /// Calls the initialization function of all systems that have (theoretically) not had Initialize called yet.
    /// </summary>
	public void InitializeSystems()
	{
		while (_initializedSystems < _systems.Count)
        {
			var system = _systems[_initializedSystems++];
            _log.Debug($"Initializing {system.GetType().Name}...");
            system.Initialize();
        }
	}


	internal void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
    	where TComp : class, IComponent
        where TEvent : struct, IEvent
        => _events.Subscribe(handler);

	public void RaiseLocalEvent<TEvent>(EntityUid entity, ref TEvent args) where TEvent : struct, IEvent
        => _events.Raise(entity, ref args);

	public void RaiseLocalEvent<TEvent>(EntityUid entity, TEvent args) where TEvent : struct, IEvent
        => _events.Raise(entity, ref args);


    /// <summary>
    /// Creates a new, blank entity.
    /// </summary>
    public EntityUid CreateEntity()
    {
        EntityUid uid = new EntityUid(_nextId++);
        _alive.Add(uid);
        return uid;
    }

    /// <summary>
    /// True if this ID refers to an alive/existing entity.
    /// </summary>
    public bool Exists(EntityUid entity) => _alive.Contains(entity);

    /// <summary>
    /// Creates an entity from a prototype, thus giving it the prototype's components.
    /// </summary>
    public EntityUid Spawn(string prototypeId)
    {
        var prototype = _prototypes.Index(prototypeId);
        if (prototype.Abstract) throw new InvalidOperationException($"Prototype '{prototypeId}' is abstract and can't be spawned.");

        /* Parents, eventually, should go here. */

        var uid = CreateEntity();
        try
        {
            var added = new List<IComponent>();
            foreach (var entry in prototype.Components.Values)
            {
                var copy = entry.Component.Clone();
                AddRaw(uid, copy);
                added.Add(copy);
            }

            /* Every entity has Metadata. If the prototype listed it's own for some reason, use that. */
            if (!TryGet<MetadataComponent>(uid, out var meta))
            {
                meta = new MetadataComponent();
                AddRaw(uid, meta);
                added.Add(meta);
            }
            meta._entityPrototype = prototype;

            /* Every entity also has a Transform, so that every entity has a place in the World, even if that's... Nowhere in particular. */
            if (!Has<TransformComponent>(uid))
            {
                var transform = new TransformComponent();
                AddRaw(uid, transform);
                added.Add(transform);
            }

            /* Only now, with everyone safely added, do we start them. */
            foreach (var component in added)
                component.LifeStatus = ComponentLifeStatus.Started;
            foreach (var component in added)
                RaiseLifecycle<ComponentStartup>(uid, component);
        }
        catch
        {
            Destroy(uid);
            throw;
        }

        return uid;
    }

    /// <summary>
    /// Gets all components from an entity.
    /// </summary>
    public IEnumerable<IComponent> GetComponents(EntityUid entity)
    {
        foreach (var store in _stores.Values)
            if (store.TryGetBoxed(entity, out var component))
                yield return component;
    }

    /// <summary>
    /// Removes an entity and all of its components.
    /// Destroying a nonexistent entity is a no-op.
    /// </summary>
    public void Destroy(EntityUid entity)
    {
        if (!_alive.Remove(entity)) return;

        List<IComponent>? stopping = null;
        foreach (var component in GetComponents(entity))
        {
            if (!component.Active) continue;
            (stopping ??= new()).Add(component);
        }

        if (stopping != null)
        {
            foreach (var component in stopping)
            {
                component.LifeStatus = ComponentLifeStatus.Stopping;
                RaiseLifecycle<ComponentShutdown>(entity, component);
            }
        }

        /* Handlers can do damn near anything, so we need to look at the stores fresh. */
        foreach (var store in _stores.Values)
        { if (store.Remove(entity, out var removed)) removed.LifeStatus = ComponentLifeStatus.Deleted; }
    }

    /// <summary>
    /// Destroy this entity at the end of the tick. Recommended over Destroy in environments with queries.
    /// </summary>
    public void QueueDestroy(EntityUid entity) => _queuedForDestruction.Add(entity);

    /// <summary>
    /// Attempts to read from the MetadataComponent of an entity to return a display name.
    /// Empty string if it has none (somehow, what the fuck).
    /// </summary>
    public string NameOf(EntityUid entity) => TryGet<MetadataComponent>(entity, out var meta) ? meta.EntityName : string.Empty;

    /// <summary>
    /// Adds a component to an entity. The entity cannot have more than one component of a certain type.
    /// </summary>
    public T Add<T>(EntityUid entity, T component) where T : class, IComponent
    {
        RequireExists(entity);
        GetOrCreateStore<T>().Add(entity, component);
        component.CreationTick = CurTick;
        component.LifeStatus = ComponentLifeStatus.Started;
        RaiseLifecycle<ComponentStartup>(entity, component);
        return component;
    }

    /// <summary>
    /// Adds a new, default-constructed component to an entity.
    /// </summary>
    public T Add<T>(EntityUid entity) where T : class, IComponent, new() => Add(entity, new T());

    /// <summary>
    /// Returns whether or not an entity has a specified component type.
    /// </summary>
    public bool Has<T>(EntityUid entity) where T : class, IComponent
        => _stores.TryGetValue(typeof(T), out var store) && ((ComponentStore<T>)store).Has(entity);

    /// <summary>
    /// Attempts to retrieve a component of a certain type from an entity.
    /// </summary>
    public bool TryGet<T>(EntityUid entity, [NotNullWhen(true)] out T? component) where T : class, IComponent
    {
        if (_stores.TryGetValue(typeof(T), out var store))
            return ((ComponentStore<T>)store).TryGet(entity, out component);

        component = null;
        return false;
    }

    /// <summary>
    /// Gets an entity's component, throwing if said entity does not have said component.
    /// </summary>
    public T Get<T>(EntityUid entity) where T : class, IComponent
        => TryGet<T>(entity, out var component) ? component : throw new InvalidOperationException($"Entity {entity} has no {typeof(T).Name} member.");

    /// <summary>
    /// Removes a component of a specified type from an entity.
    /// </summary>
    public bool Remove<T>(EntityUid entity) where T : class, IComponent
    {
        if (!TryGet<T>(entity, out var component)) return false;

        if (component.Active)
        {
            component.LifeStatus = ComponentLifeStatus.Stopping;
            RaiseLifecycle<ComponentShutdown>(entity, component);
        }

        if (!_stores.TryGetValue(typeof(T), out var store) || !store.Remove(entity, out var removed)) return false;
        removed.LifeStatus = ComponentLifeStatus.Deleted;
        return true;
    }

    public EntityPrototype? PrototypeOf(EntityUid entity)
    {
        RequireExists(entity);
        if (TryGet<MetadataComponent>(entity, out var meta))
            return meta._entityPrototype;
        return null;
    }

    /// <summary>
    /// Every entity that has T, along with it.
    /// Don't add or remove T components while looping, as that will throw...
    /// Instead, call ToArray() first if you need to.
    /// </summary>
    public IEnumerable<(EntityUid Entity, T Component)> Query<T>() where T : class, IComponent
    {
        if (!_stores.TryGetValue(typeof(T), out var store)) yield break;
        foreach (var (entity, component) in ((ComponentStore<T>)store).All)
            yield return (entity, component);
    }

    /// <summary>
    /// Every entity that has both T1 and T2.
    /// Don't add or remove T1 or T2 components while looping, as that will throw...
    /// Instead, call ToArray() first if you need to.
    /// </summary>
    public IEnumerable<(EntityUid Entity, T1 First, T2 Second)> Query<T1, T2>()
        where T1 : class, IComponent
        where T2 : class, IComponent
    {
        if (!_stores.TryGetValue(typeof(T1), out var store1) || !_stores.TryGetValue(typeof(T2), out var store2))
            yield break;

        var first  = (ComponentStore<T1>)store1;
        var second = (ComponentStore<T2>)store2;

        if (first.Count <= second.Count)
        {
            foreach (var (entity, a) in first.All)
                if (second.TryGet(entity, out var b)) yield return (entity, a, b);
        }
        else
        {
            foreach (var (entity, b) in second.All)
                if (first.TryGet(entity, out var a)) yield return (entity, a, b);
        }
    }


    private void RaiseLifecycle<TEvent>(EntityUid entity, IComponent component) where TEvent : struct, IEvent
    {
        var args = default(TEvent);
        _events.RaiseForComponent(entity, component.GetType(), ref args);
    }

    private void RequireExists(EntityUid entity)
    { if (!Exists(entity)) throw new InvalidOperationException($"Entity {entity} does not exist."); }

    private ComponentStore<T> GetOrCreateStore<T>() where T : class, IComponent
    {
        if (_stores.TryGetValue(typeof(T), out var store)) return (ComponentStore<T>)store;

        var created = new ComponentStore<T>();
        _stores.Add(typeof(T), created);
        return created;
    }

    private void AddRaw(EntityUid entity, IComponent component)
    {
        var type = component.GetType();

        if (!_stores.TryGetValue(type, out var store))
        {
            store = (IComponentStore)Activator.CreateInstance(typeof(ComponentStore<>).MakeGenericType(type))!;
            _stores.Add(type, store);
        }

        store.AddBoxed(entity, component);
        component.CreationTick = CurTick;
        component.LifeStatus = ComponentLifeStatus.Added;
    }
}
