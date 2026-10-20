using System.Diagnostics.CodeAnalysis;
using Stratosphere.Core.ECS.BuiltInComponents;

namespace Stratosphere.Core.ECS.Prototypes;

public class EntityPrototype : IPrototype, IInheritingPrototype
{
    /* Unlike RobustToolbox, we currently don't worry ourselves with localization, so... */

    /// <summary>
    /// The internal name of this prototype.
    /// </summary>
    /// <seealso cref="IPrototype.ID"/>
    [DataField("id", true, true, false)] public string ID { get; private set; } = default!;
    /// <summary>
    /// The display name of this prototype. In other words, the name the players might see it as, should the prototype exist in the world.
    /// </summary>
    [DataField("name")] public string? DisplayName { get; private set; }
    /// <summary>
    /// The description of this prototype. A basic description that players will see, should the prototype exist in the world.
    /// </summary>
    [DataField("description")] public string? Description { get; private set; }

    public string[]? Parents { get; private set; }
    public bool Abstract { get; private set; }

    /* Not very data-oriented of us. */
    [DataField("components")]
    public ComponentRegistry Components = new();

    public EntityPrototype()
    {
        /* TODO ... Something like a Transform component. */
        Components.Add("Metadata", new ComponentRegiEntry(new MetadataComponent()));
    }
}

public readonly struct ComponentRegiEntry
{
    public readonly IComponent Component;

    public ComponentRegiEntry(IComponent component) { Component = component; }
}

public sealed class ComponentRegistry : Dictionary<string, ComponentRegiEntry>
{
    public ComponentRegistry() {}
    public ComponentRegistry(Dictionary<string, ComponentRegiEntry> components) : base(components) {}

    public bool TryGetComponent(string compName, [NotNullWhen(true)] out IComponent? component)
    {
        var success = TryGetValue(compName, out var comp);
        component = success ? comp.Component : null;
        return success;
    }

    /* TODO ... Generic-type TryGetComponent signature? */
}
