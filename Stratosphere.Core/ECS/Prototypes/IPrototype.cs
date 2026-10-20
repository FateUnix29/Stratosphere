namespace Stratosphere.Core.ECS.Prototypes;

/* Taking a bit of inspiration from RobustToolbox. */


/// <summary>
/// A prototype. One of the lowest units the game can act upon.
/// </summary>
/// <seealso cref="IInheritingPrototype"/>
public interface IPrototype
{
    /// <summary>
    /// The unique ID for this specific prototype.
    /// This will never be a duplicate; Stratosphere will exit otherwise.
    /// </summary>
    string ID { get; }
}

/// <summary>
/// An extension of <see cref="IPrototype"/> capable of inheriting components and data in
/// general from other prototypes.
/// </summary>
public interface IInheritingPrototype
{
    string[]? Parents { get; }
    bool Abstract { get; }
}
