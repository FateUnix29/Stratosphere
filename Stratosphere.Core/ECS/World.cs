using System.Diagnostics.CodeAnalysis;
using Stratosphere.Core.ECS.BuiltInComponents;
using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS;

public sealed class World
{
    private readonly PrototypeManager _prototypes;
    private readonly HashSet<EntityUid> _alive = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    private long _nextId = 1;
    public GameTick CurTick { get; private set; } = GameTick.Zero;

    public int EntityCount => _alive.Count;

    public World(PrototypeManager prototypes) { _prototypes = prototypes; }

    /// <summary>
    /// Advances world by a single tick.
    /// </summary>
    public void Tick() => CurTick += 1;

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

            /* Only now, with everyone safely added, do we start them.
               Need to fire init events and whatnot here. TODO. */
            foreach (var component in added)
                component.LifeStatus = ComponentLifeStatus.Started;
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

        foreach (var store in _stores.Values)
        { if (store.Remove(entity, out var removed)) removed.LifeStatus = ComponentLifeStatus.Deleted; }
    }

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
        component.LifeStatus = ComponentLifeStatus.Started; /* Still need to do events... */
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
