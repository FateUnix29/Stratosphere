using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS.Events;

/// <summary>
/// Events are a system that allow for things to be processed as they happen instead of being checked for by constantly querying.
/// Define one as a struct implementing this interface.
/// </summary>
public interface IEvent { }

public delegate void ComponentEventHandler<TComp, TEvent>(EntityUid uid, TComp component, ref TEvent args)
    where TComp : class, IComponent
    where TEvent : struct, IEvent;
