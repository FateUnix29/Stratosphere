namespace Stratosphere.Core/*.Types*/;

public readonly struct GameTick : IEquatable<GameTick>, IComparable<GameTick>
{
    public static readonly GameTick Zero = new(0);
    public static readonly GameTick MaxValue = new(ulong.MaxValue); /* if this isn't enough move to 128 bits */

    public readonly ulong Value;

    public GameTick(ulong value) { Value = value; }

    public bool Equals(GameTick o) => Value == o.Value;
    public override bool Equals(object? o)
    {
        if (ReferenceEquals(null, o)) return false;
        return o is GameTick gt && Equals(gt);
    }

    /* RobustToolbox does this, so i guess i will too...? this is very... questionable given its a ulong. */
    public override int GetHashCode() => (int)Value;
    public int CompareTo(GameTick o) => Value.CompareTo(o.Value);

    public static bool operator ==(GameTick a, GameTick b) => a.Value == b.Value;
    public static bool operator !=(GameTick a, GameTick b) => a.Value != b.Value;
    public static bool operator  >(GameTick a, GameTick b) => a.Value > b.Value;
    public static bool operator >=(GameTick a, GameTick b) => a.Value >= b.Value;
    public static bool operator  <(GameTick a, GameTick b) => a.Value < b.Value;
    public static bool operator <=(GameTick a, GameTick b) => a.Value <= b.Value;

    public static GameTick operator +(GameTick a, ulong b) => new(a.Value + b);
    public static GameTick operator -(GameTick a, ulong b) => new(a.Value - b);

    public override string ToString() => Value.ToString();
}
