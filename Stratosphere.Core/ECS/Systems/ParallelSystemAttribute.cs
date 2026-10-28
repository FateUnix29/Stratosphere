namespace Stratosphere.Core.ECS.Systems;


/// <summary>
/// Marks a system as safe to run in parallel with other systems, and what it needs in order to be safe.
/// When this is not present, a system runs by itself in order, and can do anything.
/// When this *is* present, the World is free to run it on another thread.
/// </summary>
/// <remarks>
/// <para>
/// List every component type the system needs to look at in <see cref="Reads"/>, and every component type it changes in <see cref="Writes"/>.
/// Two systems that don't clash on these can run at the same time.
/// When they do clash, they must run one after another, in the same order that they'd have without parallelism.
/// This is still faster than just... No parallelism at all, even if many systems might clash. Because, in theory, some won't.
/// Declarations don't change what happens, just where... Do it correctly, and threads should be invisible to you!
/// </para>
/// <para>
/// Parallel systems must also follow these rules.
/// - Touch only the declared component types, their own fields, and things no-one else changes during that tick.
/// - No direct structural changes such as spawning, destroying, component addition/removal, raising events, etc. They are instead queued with <c>Commands</c>.
/// - Other systems' states are off limits unless they're read-only for the entire tick.
/// The World will try to check the first two while <see cref="World.CheckSystemAccess"/> is enabled, which, it is on debug builds. It can notice a component type
/// wasn't declared, but it can't notice that a system declares a Read and then writes to something that it's only supposed to be reading. Be honest.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ParallelSystemAttribute : Attribute
{
    public Type[] Reads { get; init; } = Array.Empty<Type>();

    /// <summary>Anything written may also be read (implies reading).</summary>
    public Type[] Writes { get; init; } = Array.Empty<Type>();
}
