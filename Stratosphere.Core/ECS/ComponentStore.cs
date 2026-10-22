using System.Diagnostics.CodeAnalysis;
using Stratosphere.Core.ECS.Prototypes;
namespace Stratosphere.Core.ECS;

/// <summary>
/// Generally something the World can use without knowing the types of what it's working with.
/// </summary>
internal interface IComponentStore
{
    int Count { get; }

    /// <summary>
    /// Adds a component whose type is only known at runtime.
    /// For example, from a prototype.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="component"></param>
    void AddBoxed(EntityUid entity, IComponent component);

    /// <summary>
    /// Used for retrieving components without knowing the type of the ComponentStore at runtime.
    /// </summary>
    bool TryGetBoxed(EntityUid entity, [NotNullWhen(true)] out IComponent? component);

    /// <summary>
    /// Removes this entity's component from the store, if it has said component.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="removed"></param>
    bool Remove(EntityUid entity, [NotNullWhen(true)] out IComponent? removed);
}

/// <summary>
/// Holds every component of a type, keyed by entity that owns them.
/// </summary>
/// <remarks>
/// Pretty much just a custom dictionary...
/// Though, nothing outside this file knows that, so we can change it later if we keep the same general structure.
/// </remarks>
internal sealed class ComponentStore<T> : IComponentStore where T: class, IComponent
{
    private readonly Dictionary<EntityUid, T> _data = new();

    public int Count => _data.Count;

    public void Add(EntityUid entity, T component)
    {
        if (!_data.TryAdd(entity, component))
            /* Should we really be throwing here? Well, I guess they can catch it. */
            throw new InvalidOperationException($"Entity {entity} already has a {typeof(T).Name}.");
    }

    public void AddBoxed(EntityUid entity, IComponent component) => Add(entity, (T)component);
    public bool TryGetBoxed(EntityUid entity, [NotNullWhen(true)] out IComponent? component)
    {
        var success = _data.TryGetValue(entity, out var found);
        component = found;
        return success;
    }

    public bool Has(EntityUid entity) => _data.ContainsKey(entity);
    public bool TryGet(EntityUid entity, [NotNullWhen(true)] out T? component) => _data.TryGetValue(entity, out component);
    public bool Remove(EntityUid entity, [NotNullWhen(true)] out IComponent? removed)
    {
        var success = _data.Remove(entity, out var component);
        removed = component;
        return success;
    }

    /// <summary>
    /// Every entity-component pair.
    /// Adding or removing components of this type while looping over this throws, so probably copy the results first.
    /// </summary>
    public IEnumerable<KeyValuePair<EntityUid, T>> All => _data;
}
