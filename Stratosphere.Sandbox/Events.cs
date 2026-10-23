using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Events;

namespace Stratosphere.Sandbox;

public struct MoveAttemptEvent : IEvent
{
    public int Dx, Dy;
    public bool Moved;
    public EntityUid Blocker;
}

public struct UseEvent : IEvent { }
public struct DropEvent : IEvent { }

public struct SwingEvent : IEvent
{
    public EntityUid User;
    public bool Hit;
}

public struct ThrowEvent : IEvent { public EntityUid User; }

public struct DamageEvent : IEvent
{
    public float Amount;
    public EntityUid Source;
}

public struct DeathEvent : IEvent { }
