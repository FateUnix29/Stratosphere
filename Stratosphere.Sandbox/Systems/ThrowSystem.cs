using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem, UpdatesAfter(typeof(HandSystem))]
[ParallelSystem(Reads=[typeof(HealthComponent)], Writes=[typeof(PositionComponent), typeof(ThrowableComponent), typeof(ItemComponent), typeof(PlayerComponent), typeof(SolidComponent)])]
public sealed class ThrowSystem : EntitySystem
{
    private GameStateSystem _state = null!;
    private CellIndexSystem _cells = null!;

    public override void Initialize()
    {
        _state = World.GetSystem<GameStateSystem>();
        _cells = World.GetSystem<CellIndexSystem>();
        SubscribeLocalEvent<ThrowableComponent, ThrowEvent>(OnThrow);
    }

    private void OnThrow(EntityUid item, ThrowableComponent thrown, ref ThrowEvent args)
    {
        if (!World.TryGet<FacingComponent>(args.User, out var facing)) return;

        thrown.DirX = facing.X;
        thrown.DirY = facing.Y;
        thrown.TimeRemaining = thrown.MaxDistance;
        thrown.HasBeenThrown = true;

        if (World.TryGet<ItemComponent>(item, out var held)) held.Hand = EntityUid.INVALID;
        if (World.TryGet<PlayerComponent>(args.User, out var player) && player.HeldItem == item)
            player.HeldItem = EntityUid.INVALID;
        _cells.Invalidate();

        if (World.TryGet<PositionComponent>(item, out var position))
            TryHit(item, thrown, position.X, position.Y);
    }

    public override void Update(float deltaTime)
    {
        foreach (var (item, thrown, position) in World.Query<ThrowableComponent, PositionComponent>().ToArray())
        {
            if (!thrown.HasBeenThrown) continue;

            int x = position.X + thrown.DirX;
            int y = position.Y + thrown.DirY;

            if (thrown.TimeRemaining <= 0 || !_state.InBounds(x, y))
            {
                Commands.Destroy(item);
                continue;
            }

            if (TryHit(item, thrown, x, y)) continue;

            position.X = x;
            position.Y = y;
            thrown.TimeRemaining--;
            _cells.Invalidate();
        }
    }

    private bool TryHit(EntityUid item, ThrowableComponent thrown, int x, int y)
    {
        var target = _cells.SolidAt(x, y, item);
        if (!target.IsValid()) return false;

        if (World.Has<HealthComponent>(target) && !World.Has<PlayerComponent>(target))
        {
            var damage = new DamageEvent { Amount = thrown.ThrowDamage, Source = item };
            Commands.Raise(target, damage);
        }

        Commands.Destroy(item);
        return true;
    }
}
