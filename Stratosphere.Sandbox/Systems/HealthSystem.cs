using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem, UpdatesAfter(typeof(ThrowSystem), typeof(EnemyAISystem), typeof(RegenSystem))]
[ParallelSystem(Writes=[typeof(HealthComponent), typeof(RegenComponent)])]
public sealed class HealthSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<HealthComponent, DamageEvent>(OnDamage);

    private void OnDamage(EntityUid uid, HealthComponent health, ref DamageEvent args)
    {
        health.Hp -= args.Amount;

        if (World.TryGet<RegenComponent>(uid, out var regen))
            regen.LastDamage = World.CurTick;
    }

    public override void Update(float deltaTime)
    {
        foreach (var (uid, health) in World.Query<HealthComponent>().ToArray())
        {
            if (health.Hp > 0) continue;

            var death = new DeathEvent();
            Commands.Raise(uid, death);
            Commands.Destroy(uid);
        }
    }
}

[RegisterSystem, ParallelSystem(Reads=[typeof(RegenComponent)], Writes=[typeof(HealthComponent)])]
public sealed class RegenSystem : EntitySystem
{
    public override void Update(float deltaTime)
    {
        foreach (var (_, health, regen) in World.Query<HealthComponent, RegenComponent>())
        {
            ulong quietFor = World.CurTick.Value - regen.LastDamage.Value;
            if (quietFor > (ulong) regen.Delay)
                health.Hp = Math.Min(health.MaxHp, health.Hp + regen.Amount);
        }
    }
}
