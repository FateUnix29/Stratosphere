namespace Stratosphere.Core.ECS.Prototypes;

public abstract partial class Component : IComponent
{
    public ComponentLifeStatus LifeStatus { get; internal set; } = ComponentLifeStatus.Allocated;
    ComponentLifeStatus IComponent.LifeStatus
    {
        get => LifeStatus;
        set => LifeStatus = value;
    }

    public bool Initialized => LifeStatus == ComponentLifeStatus.Initializing || LifeStatus == ComponentLifeStatus.Initialized;
    public bool Active => LifeStatus == ComponentLifeStatus.Starting || LifeStatus == ComponentLifeStatus.Started;
    public bool Deleted => LifeStatus == ComponentLifeStatus.Deleting || LifeStatus == ComponentLifeStatus.Deleted;

    public GameTick CreationTick { get; internal set; }
    GameTick IComponent.CreationTick
    {
        get => CreationTick;
        set => CreationTick = value;
    }

    /// <summary>
    /// A shallow-copy of the Component.
    /// Value fields are copied, but fields with lists, dictionaries, and the likes will still point to the same object.
    /// Treat them as read-only, or override this!
    /// </summary>
    public virtual IComponent Clone()
    {
        var copy = (IComponent)MemberwiseClone();
        copy.LifeStatus = ComponentLifeStatus.Allocated;
        copy.CreationTick = GameTick.Zero;
        return copy;
    }
}
