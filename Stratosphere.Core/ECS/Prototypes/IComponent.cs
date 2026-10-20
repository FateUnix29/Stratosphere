namespace Stratosphere.Core.ECS.Prototypes;

public enum ComponentLifeStatus : byte
{
    Allocated,
    Adding, Added,
    Initializing, Initialized,
    Starting, Started,
    Stopping, Stopped,
    Deleting, Deleted
}

/// <summary>
/// The basic interface for a component in the ECS. Attached to prototypes at the hip.
/// </summary>
public interface IComponent
{
    /* TODO: Networking. */

    ComponentLifeStatus LifeStatus { get; internal set; }

    /// <summary>
    /// True if the component has initialized or is undergoing initialization.
    /// </summary>
    bool Initialized { get; }

    /// <summary>
    /// True when the component is 'active'/running.
    /// </summary>
    bool Active { get; }

    /// <summary>
    /// True if this component should no longer exist/has been removed from its owner...
    /// </summary>
    bool Deleted { get; }

    /// <summary>
    /// The tick the component was created.
    /// </summary>
    GameTick CreationTick { get; internal set; }
}
