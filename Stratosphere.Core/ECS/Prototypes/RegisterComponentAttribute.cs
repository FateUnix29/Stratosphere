namespace Stratosphere.Core.ECS.Prototypes;

/// <summary>
/// Marks a class as component that YAML can refer to by name.
/// More specifically, makes it show up when <see cref="ComponentFactory"/> scans an assembly.
/// </summary>
/// <remarks>
/// By default, name is class name minus a 'Component' prefix. SpriteComponent -> Sprite
/// Pass a name to override.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RegisterComponentAttribute : Attribute
{
    /// <summary>
    /// The name used in YAML, or null to default.
    /// </summary>
    public string? Name { get; }
    public RegisterComponentAttribute(string? name = null) { Name = name; }
}