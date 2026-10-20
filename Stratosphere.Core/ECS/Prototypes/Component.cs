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
}

/* TODO ... Events. */
