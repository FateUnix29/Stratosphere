using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem]
public sealed class SwingSystem : EntitySystem
{
    private CellIndexSystem _cells = null!;
    private HandSystem _hands = null!;

    public override void Initialize()
    {
        _cells = World.GetSystem<CellIndexSystem>();
        _hands = World.GetSystem<HandSystem>();
        SubscribeLocalEvent<SwingableComponent, SwingEvent>(OnSwing);
    }

    private void OnSwing(EntityUid item, SwingableComponent swing, ref SwingEvent args)
    {
        if (!_hands.TryFrontCell(args.User, out var x, out var y)) return;

        foreach (var target in _cells.At(x, y).ToArray())
        {
            if (!World.Has<HealthComponent>(target) || World.Has<PlayerComponent>(target)) continue;

            var damage = new DamageEvent { Amount = swing.SwingDamage, Source = item };
            World.RaiseLocalEvent(target, ref damage);
            args.Hit = true;
        }
    }
}