using System.Text;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.BuiltInComponents;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem]
public sealed class ConsoleRenderSystem : EntitySystem
{
    private GameStateSystem _state = null!;

    public override void Initialize() => _state = World.GetSystem<GameStateSystem>();

    public string Render()
    {
        int width = _state.SizeX, height = _state.SizeY;
        var chars = new char[height, width];
        var order = new int[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                chars[y,x] = '.';
                order[y,x] = int.MinValue;
            }

        if (World.TryGet<PositionComponent>(_state.Player, out var playerPos)
            && World.TryGet<FacingComponent>(_state.Player, out var facing))
        {
            int fx = playerPos.X + facing.X, fy = playerPos.Y + facing.Y;
            if (_state.InBounds(fx, fy))
            {
                chars[fy, fx] = '@';
                order[fy, fx] = -1;
            }
        }

        foreach (var (entity, position, glyph) in World.Query<PositionComponent, GlyphComponent>())
        {
            if (!_state.InBounds(position.X, position.Y) || IsHidden(entity)) continue;
            if (glyph.DrawOrder >= order[position.Y, position.X])
            {
                chars[position.Y, position.X] = glyph.Char;
                order[position.Y, position.X] = glyph.DrawOrder;
            }
        }

        var text = new StringBuilder(width * height + height + 200);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) text.Append(chars[y,x]);
            text.Append('\n');
        }

        text.Append('\n');
        if (World.TryGet<HealthComponent>(_state.Player, out var health))
            text.Append($"HP {Math.Ceiling(health.Hp)}/{health.MaxHp}   ");
        text.Append($"Tick {World.CurTick}\n");
        text.Append("WASD to move, E to pick up or use, Q to drop, and Esc to quit.\n");

        if (_state.InspectName.Length > 0 || _state.InspectDesc.Length > 0)
            text.Append($"\n{_state.InspectName} | {_state.InspectDesc}");

        return text.ToString();
    }

    private bool IsHidden(EntityUid entity)
        => World.TryGet<TagsComponent>(entity, out var tags)
        && tags.Tags.TryGetValue("doNotRenderIfHeld", out var hide)
        && World.TryGet<ItemComponent>(entity, out var item) && item.Hand.IsValid();
}
