using Stratosphere.Core;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Sandbox;

[RegisterComponent]
public sealed class PositionComponent : Component
{
    [DataField("x")] public int X;
    [DataField("y")] public int Y;
}

[RegisterComponent]
public sealed class FacingComponent : Component
{
    [DataField("x")] public int X;
    [DataField("y")] public int Y = -1;
}

/* marks how to draw something */
[RegisterComponent]
public sealed class GlyphComponent : Component
{
    [DataField("char")] public char Char = '?';
    [DataField("drawOrder")] public int DrawOrder;
}

/* cannot be walked through */
[RegisterComponent]
public sealed class SolidComponent : Component { }


[RegisterComponent]
public sealed class HealthComponent : Component
{
    [DataField("hp")] public float Hp = 100;
    [DataField("maxHp")] public float MaxHp = 100;
}

/* amount of regeneration every tick once delay ticks have passed */
[RegisterComponent]
public sealed class RegenComponent : Component
{
    [DataField("amount")] public float Amount = 10;
    [DataField("delay")] public int Delay = 3;
    public GameTick LastDamage;
}


[RegisterComponent]
public sealed class PlayerComponent : Component
{
    public EntityUid HeldItem = EntityUid.INVALID;
}

[RegisterComponent]
public sealed class EnemyComponent : Component
{
    [DataField("attackDamage")] public float AttackDamage = 10;
    /* moves once every this many ticks */
    [DataField("moveEvery")] public int MoveEvery = 2;
}

[RegisterComponent]
public sealed class ItemComponent : Component
{
    public EntityUid Hand = EntityUid.INVALID;
}

[RegisterComponent]
public sealed class SwingableComponent : Component
{
    [DataField("swingDamage")] public float SwingDamage;
}

[RegisterComponent]
public sealed class ThrowableComponent : Component
{
    [DataField("throwDamage")] public float ThrowDamage;
    [DataField("range")] public int MaxDistance = 7;

    public bool HasBeenThrown;
    public int TimeRemaining;
    public int DirX;
    public int DirY;
}

[RegisterComponent]
public sealed class TagsComponent : Component
{
    [DataField("tags")] public required Dictionary<string, object> Tags;
}
