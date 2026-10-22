using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS.Systems;

public abstract partial class EntitySystem : IEntitySystem
{
	protected World World { get; private set; } = null!;
	internal void Attach(World world) => World = world;
	
    public abstract void Initialize();
    public abstract void Update(float deltaTime);

    protected void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
    	where TComp :  class,  IComponent
    	where TEvent : struct, IEvent
    	=> World.SubscribeLocalEvent(handler);

    protected void RaiseLocalEvent<TEvent>(EntityUid entity, ref TEvent args) where TEvent : struct, IEvent
    	=> World.RaiseLocalEvent(entity, ref args);

	protected void RaiseLocalEvent<TEvent>(EntityUid entity, TEvent args) where TEvent : struct, IEvent
		=> World.RaiseLocalEvent(entity, args);
}
