using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS.Events;

internal sealed class EventBus
{
	private readonly World _world;
	private readonly Dictionary<Type, object> _subscriptions = new();

	public EventBus(World world) { _world = world; }

	public void Subscribe<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
    	where TComp : class, IComponent
	    where TEvent : struct, IEvent
	{
		if (!_subscriptions.TryGetValue(typeof(TEvent), out var existing))
	   	{
	    	existing = new List<EventSubscription<TEvent>>();
	        _subscriptions.Add(typeof(TEvent), existing);
	    }

	    ((List<EventSubscription<TEvent>>) existing).Add(new ComponentEventSubscription<TComp, TEvent>(handler));
	}

	public void Raise<TEvent>(EntityUid entity, ref TEvent args) where TEvent : struct, IEvent
	{
		if (!_subscriptions.TryGetValue(typeof(TEvent), out var existing)) return;

		var list = (List<EventSubscription<TEvent>>)existing;

		int count = list.Count;
		for (int i = 0; i < count; i++)
			list[i].Invoke(_world, entity, ref args);
	}
}

internal abstract class EventSubscription<TEvent> where TEvent : struct, IEvent
{
	public abstract void Invoke(World world, EntityUid entity, ref TEvent args);
}

internal sealed class ComponentEventSubscription<TComp, TEvent> : EventSubscription<TEvent>
	where TComp  : class,  IComponent
	where TEvent : struct, IEvent
{
	private readonly ComponentEventHandler<TComp, TEvent> _handler;

	public ComponentEventSubscription(ComponentEventHandler<TComp, TEvent> handler) { _handler = handler; }

	public override void Invoke(World world, EntityUid entity, ref TEvent args)
	{
		if (world.TryGet<TComp>(entity, out var component))
			_handler(entity, component, ref args);
	}
}
