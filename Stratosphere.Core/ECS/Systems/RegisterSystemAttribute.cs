namespace Stratosphere.Core.ECS.Systems;

/// <summary>
/// Marks a system to be created and added when World scans the assembly for systems. (<see cref="World.AddSystemsFromAssembly"/>)
/// The system needs a public parameterless constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RegisterSystemAttribute : Attribute { }


/// <summary>
/// This system must update *after* the passed systems. Only matters between systems that are added together in one scan pass/batch.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UpdatesAfterAttribute : Attribute
{
    public Type[] Systems { get; }
    public UpdatesAfterAttribute(params Type[] systems) { Systems = systems; }
}


/// <summary>
/// This system must update *before* the passed systems. Only matters between systems that are added together in one scan pass/batch.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UpdatesBeforeAttribute : Attribute
{
    public Type[] Systems { get; }
    public UpdatesBeforeAttribute(params Type[] systems) { Systems = systems; }
}
