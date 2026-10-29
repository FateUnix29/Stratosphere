using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem, UpdatesAfter(typeof(ThrowSystem))]
[ParallelSystem(Reads=[typeof(EnemyComponent), typeof(PositionComponent)])]
public sealed class EnemyAISystem : EntitySystem
{
    private GameStateSystem _state = null!;
    public override void Initialize() => _state = World.GetSystem<GameStateSystem>();
    public override void Update(float deltaTime)
    {
        if (!World.TryGet<PositionComponent>(_state.Player, out var target)) return;

        foreach (var (enemyUid, enemy, pos) in World.Query<EnemyComponent, PositionComponent>().ToArray())
        {
            int dx = Math.Sign(target.X - pos.X);
            int dy = Math.Sign(target.Y - pos.Y);

            /* if next step lands on player, hit */
            if ((dx != 0 || dy != 0) && target.X == pos.X + dx && target.Y == pos.Y + dy)
            {
                var damage = new DamageEvent { Amount = enemy.AttackDamage, Source = enemyUid };
                /*World.RaiseLocalEvent(_state.Player, ref damage);*/
                Commands.Raise(_state.Player, damage);
            }

            if (World.CurTick.Value % (ulong)Math.Max(1, enemy.MoveEvery) != 0) continue;

            var stepX = new MoveAttemptEvent { Dx = dx }; Commands.Raise(enemyUid, stepX);
            var stepY = new MoveAttemptEvent { Dy = dy }; Commands.Raise(enemyUid, stepY);
        }
    }
}

/* Not possible to make this parallel. */
[RegisterSystem, UpdatesAfter(typeof(EnemyAISystem))]
public sealed class EnemySpawnSystem : EntitySystem
{
    private const ulong SpawnEvery = 15;

    private GameStateSystem _state = null!;
    private CellIndexSystem _cells = null!;

    public override void Initialize()
    {
        _state = World.GetSystem<GameStateSystem>();
        _cells = World.GetSystem<CellIndexSystem>();
    }

    public override void Update(float deltaTime)
    {
        if (!_state.SpawnEnemies) return;
        if (World.CurTick.Value % SpawnEvery != 0 || _state.Rng.Next(2) != 0) return;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int x = _state.Rng.Next(_state.SizeX);
            int y = _state.Rng.Next(_state.SizeY);
            if (_cells.SolidAt(x, y).IsValid()) continue;

            var enemy = World.Spawn("enemy");
            var position = World.Get<PositionComponent>(enemy);
            position.X = x; position.Y = y;
            _cells.Invalidate();
            return;
        }
    }
}