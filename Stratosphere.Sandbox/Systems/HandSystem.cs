using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem, ParallelSystem(Reads=[typeof(PlayerComponent), typeof(ItemComponent), typeof(FacingComponent)], Writes=[typeof(PositionComponent)])]
public sealed class HandSystem : EntitySystem
{
    private GameStateSystem _state = null!;
    private CellIndexSystem _cells = null!;

    public override void Initialize()
    {
        _state = World.GetSystem<GameStateSystem>();
        _cells = World.GetSystem<CellIndexSystem>();
        SubscribeLocalEvent<PlayerComponent, UseEvent>(OnUse);
        SubscribeLocalEvent<PlayerComponent, DropEvent>(OnDrop);
    }

    public bool TryFrontCell(EntityUid holder, out int x, out int y)
    {
        x = y = 0;
        if (!World.TryGet<PositionComponent>(holder, out var position) || !World.TryGet<FacingComponent>(holder, out var facing))
            return false;

        x = position.X + facing.X;
        y = position.Y + facing.Y;
        return _state.InBounds(x, y);
    }

    private void OnUse(EntityUid uid, PlayerComponent player, ref UseEvent args)
    {
        if (player.HeldItem.IsValid())
        {
            var swing = new SwingEvent { User = uid };
            Commands.Raise(player.HeldItem, swing);

            var toss = new ThrowEvent { User = uid };
            Commands.Raise(player.HeldItem, toss);
            return;
        }

        if (!TryFrontCell(uid, out var x, out var y)) return;

        foreach (var candidate in _cells.At(x, y))
        {
            if (!World.TryGet<ItemComponent>(candidate, out var item)) continue;

            player.HeldItem = candidate;
            item.Hand = uid;
            _cells.Invalidate();
            return;
        }
    }

    private void OnDrop(EntityUid uid, PlayerComponent player, ref DropEvent args)
    {
        if (!player.HeldItem.IsValid()) return;

        if (World.TryGet<ItemComponent>(player.HeldItem, out var item))
            item.Hand = EntityUid.INVALID;

        player.HeldItem = EntityUid.INVALID;
        _cells.Invalidate();
    }

    public override void Update(float deltaTime)
    {
        foreach (var (holder, player) in World.Query<PlayerComponent>())
        {
            if (player.HeldItem.IsValid()
                && World.TryGet<PositionComponent>(player.HeldItem, out var itemPosition)
                && TryFrontCell(holder, out var x, out var y))
            {
                itemPosition.X = x;
                itemPosition.Y = y;
            }
        }
    }
}
