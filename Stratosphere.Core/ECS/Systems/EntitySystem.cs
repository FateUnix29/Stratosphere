using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.Logging;

namespace Stratosphere.Core.ECS.Systems;

public abstract partial class EntitySystem : IEntitySystem
{
	protected World World { get; private set; } = null!;
	internal void Attach(World world) => World = world;

	private Sawmill? _log;

	internal bool IsParallel { get; set; }
	internal CommandBuffer CommandsBuffer { get; } = new();

	/// <summary>
	/// Changes to the world, such as spawning, destroying, adding and removing components, and raising events, to be made later, at the next sync point.
	/// Required in parallel systems, and fine to use anywhere.
	/// See <see cref="CommandBuffer"/>.
	/// </summary>
	protected CommandBuffer Commands => CommandsBuffer;

	/// <summary>
	/// Runs <paramref name="body"/> for every entity with a T, spread across the threads. Only touch the entity you're given.
	/// </summary>
	protected void ParallelForEach<T>(Action<EntityUid, T> body, int chunkSize = 64) where T : class, IComponent
		=> World.ParallelForEach(body, chunkSize);

	protected void ParallelForEach<T>(Action<EntityUid, T, CommandBuffer> body, int chunkSize = 64) where T : class, IComponent
		=> World.ParallelForEach(body, Commands, chunkSize);

	protected void ParallelForEach<T1, T2>(Action<EntityUid, T1, T2> body, int chunkSize = 64)
		where T1 : class, IComponent
		where T2 : class, IComponent
		=> World.ParallelForEach(body, chunkSize);

	protected void ParallelForEach<T1, T2>(Action<EntityUid, T1, T2, CommandBuffer> body, int chunkSize = 64)
		where T1 : class, IComponent
		where T2 : class, IComponent
		=> World.ParallelForEach(body, Commands, chunkSize);

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
