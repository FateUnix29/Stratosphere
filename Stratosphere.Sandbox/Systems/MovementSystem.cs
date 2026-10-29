using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem, ParallelSystem(Writes=[typeof(PositionComponent), typeof(FacingComponent)])]
public sealed class MovementSystem : EntitySystem
{
    private GameStateSystem _state = null!;
    private CellIndexSystem _cells = null!;

    public override void Initialize()
    {
        _state = World.GetSystem<GameStateSystem>();
        _cells = World.GetSystem<CellIndexSystem>();
        SubscribeLocalEvent<PositionComponent, MoveAttemptEvent>(OnMoveAttempt);
    }

    private void OnMoveAttempt(EntityUid uid, PositionComponent position, ref MoveAttemptEvent args)
    {
        if (args.Dx == 0 && args.Dy == 0) return;

        if (World.TryGet<FacingComponent>(uid, out var facing))
        {
            facing.X = Math.Sign(args.Dx);
            facing.Y = Math.Sign(args.Dy);
        }

        int x = position.X + args.Dx;
        int y = position.Y + args.Dy;
        if (!_state.InBounds(x, y)) return;

        var blocker = _cells.SolidAt(x, y, uid);
        if (blocker.IsValid())
        {
            args.Blocker = blocker;
            return;
        }

        position.X = x;
        position.Y = y;
        args.Moved = true;
        _cells.Invalidate();
    }
}
