using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.Logging;

namespace Stratosphere.Core.ECS.Systems;

public abstract partial class EntitySystem : IEntitySystem
{
	protected World World { get; private set; } = null!;
	internal void Attach(World world) => World = world;

	private Sawmill? _log;

	protected Sawmill Log => _log ??= World.Logs.GetSawmill(GetType().Name);

    public virtual void Initialize() {}
    public virtual void Update(float deltaTime) {}

    protected void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
    	where TComp :  class,  IComponent
    	where TEvent : struct, IEvent
    	=> World.SubscribeLocalEvent(handler);

    protected void RaiseLocalEvent<TEvent>(EntityUid entity, ref TEvent args) where TEvent : struct, IEvent
    	=> World.RaiseLocalEvent(entity, ref args);

	protected void RaiseLocalEvent<TEvent>(EntityUid entity, TEvent args) where TEvent : struct, IEvent
		=> World.RaiseLocalEvent(entity, args);
}
