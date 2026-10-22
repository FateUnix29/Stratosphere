using System.Diagnostics.CodeAnalysis;

namespace Stratosphere.Core.ECS.Prototypes;

/// <summary>
/// Handles the loading and management of entity prototypes.
/// </summary>
public sealed partial class PrototypeManager
{
    /* Should try to stay relatively data oriented... */

    private readonly Dictionary<string, EntityPrototype> _entities = new();

    /// <summary>
    /// Registers a prototype. Duplicate IDs will ArgumentException.
    /// </summary>
    public void Register(EntityPrototype prototype)
    {
        /* my instinct is to use IsNullOrWhitespace but honestly if you manage to get a whitespace id from yml congratulations you deserve it to work */
        if (string.IsNullOrEmpty(prototype.ID))
            throw new ArgumentException("Prototypes must have an ID.");
        if (prototype.Parents is { Length: > 0 })
            throw new ArgumentException($"Prototype '{prototype.ID}' lists parents, but parents are only resolved for prototypes loaded from YAML.");
        if (!_entities.TryAdd(prototype.ID, prototype))
            throw new ArgumentException($"Duplicate entity prototype ID '{prototype.ID}'.");
    }

    public bool TryIndex(string id, [NotNullWhen(true)] out EntityPrototype? prototype) => _entities.TryGetValue(id, out prototype);

    public EntityPrototype Index(string id)
        => _entities.TryGetValue(id, out var prototype)
        ? prototype
        : throw new KeyNotFoundException($"Unknown entity prototype '{id}'."
            + (_pending.Count > 0 ? $" ({_pending.Count} YAML prototypes are loaded but not resolved yet; call Resolve().)" : ""));
}
