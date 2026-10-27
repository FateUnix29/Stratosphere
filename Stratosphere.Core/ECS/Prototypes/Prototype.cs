using System.Diagnostics.CodeAnalysis;
using Stratosphere.Core.ECS.Components;

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

    /// <summary>
    /// The IDs of prototypes this one derives/inherits from.
    /// </summary>
    [DataField("parents")] public string[]? Parents { get; private set; }

    /// <summary>
    /// Abstract prototypes only exist to be derived from, and cannot be spawned.
    /// </summary>
    [DataField("abstract")] public bool Abstract { get; private set; }

    /* Not very data-oriented of us. */
    [DataField("components")]
    public ComponentRegistry Components = new();

    /// <summary>
    /// For the YAML loader/deserializer.
    /// </summary>
    public EntityPrototype() {}

    /// <summary>
    /// For constructing a prototype manually in code (for testing and the likes).
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="parents"></param>
    /// <param name="isAbstract"></param>
    public EntityPrototype(string id, string? name = null, string? description = null, string[]? parents = null, bool isAbstract = false)
    {
        ID = id;
        DisplayName = name;
        Description = description;
        Parents = parents;
        Abstract = isAbstract;
    }

    /// <summary>
    /// Adds a component template to the internal dictionary.
    /// </summary>
    /// <param name="component"></param>
    public EntityPrototype With(IComponent component)
    {
        /* every entity gets a MetadataComponent, regardless of if the prototype defined it or not */
        Components.Add(ComponentNames.Of(component.GetType()), new ComponentRegiEntry(component));
        return this;
    }
}


public static class ComponentNames
{
    public static string Of(Type componentType)
    {
        const string suffix = "Component"; /* MetadataComponent becomes Metadata */
        var name = componentType.Name;

        return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            ? name[..^suffix.Length] : name;
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
