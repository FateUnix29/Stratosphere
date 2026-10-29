using Stratosphere.Core;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Systems;

namespace Stratosphere.Sandbox;

[RegisterSystem]
[ParallelSystem(Reads=[typeof(PositionComponent), typeof(ItemComponent), typeof(SolidComponent)])]
public sealed class CellIndexSystem : EntitySystem
{
    private static readonly List<EntityUid> Nothing = new();
    private readonly Dictionary<(int X, int Y), List<EntityUid>> _cells = new();
    private bool _valid;
    private GameTick _builtAt;

    public void Invalidate() => _valid = false;

    public IReadOnlyList<EntityUid> At(int x, int y)
    {
        Refresh();
        return _cells.TryGetValue((x, y), out var found) ? found : Nothing;
    }

    public EntityUid SolidAt(int x, int y, EntityUid ignore = default)
    {
        foreach (var ent in At(x, y))
            if (ent != ignore && World.Has<SolidComponent>(ent))
                return ent;

        return EntityUid.INVALID;
    }

    private void Refresh()
    {
        if (_valid && _builtAt == World.CurTick) return;

        _cells.Clear();
        foreach (var (ent, pos) in World.Query<PositionComponent>())
        {
            if (World.TryGet<ItemComponent>(ent, out var item) && item.Hand.IsValid()) continue;
            if (!_cells.TryGetValue((pos.X, pos.Y), out var list))
                _cells.Add((pos.X, pos.Y), list = new List<EntityUid>());
            list.Add(ent);
        }

        _valid = true;
        _builtAt = World.CurTick;
    }
}
