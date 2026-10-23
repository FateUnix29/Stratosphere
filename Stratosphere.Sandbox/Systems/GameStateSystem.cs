using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.BuiltInComponents;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem]
public sealed class GameStateSystem : EntitySystem
{
    public int SizeX { get; private set; } = 100;
    public int SizeY { get; private set; } = 50;
    public Random Rng { get; private set; } = new();

    public EntityUid Player { get; set; } = EntityUid.INVALID;
    public bool Running { get; private set; } = true;

    public bool SpawnEnemies { get; set; } = true;

    public string InspectName { get; private set; } = "";
    public string InspectDesc { get; private set; } = "";

    public void Configure(int sizeX, int sizeY, int? seed)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        Rng = seed is { } s ? new Random(s) : new Random();
    }

    public override void Initialize()
        => SubscribeLocalEvent<PlayerComponent, DeathEvent>(OnPlayerDeath);

    private void OnPlayerDeath(EntityUid uid, PlayerComponent player, ref DeathEvent args) => Running = false;
    public void Quit() => Running = false;
    public bool InBounds(int x, int y) => x >= 0 && x < SizeX && y >= 0 && y < SizeY;
    public void ClearInspect() => InspectName = InspectDesc = "";
    public void Inspect(EntityUid entity)
    {
        InspectName = World.NameOf(entity);
        InspectDesc = World.TryGet<MetadataComponent>(entity, out var meta) ? meta.EntityDescription : "";
    }
}
