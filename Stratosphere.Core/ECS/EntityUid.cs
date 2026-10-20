namespace Stratosphere.Core.ECS;

/// <summary>
/// A dedicated type for an entity's unique identifier.
/// </summary>
public readonly struct EntityUid : IEquatable<EntityUid>, IComparable<EntityUid>
{
    public readonly long Id;
 
    /// <summary>
    /// An invalid UID to compare against.
    /// </summary>
    public static readonly EntityUid INVALID = new(0);
 
    public EntityUid(long id) { Id = id; }

    /// <summary>
    /// Whether or not this ID is valid.
    /// Does not tell you if it refers to an actual entity.
    /// </summary>
    public bool Valid => IsValid();

    /// <summary>
    /// Checks if this ID is valid (above 0).
    /// </summary>
    /// <returns>true if valid, false if not.</returns>
    public bool IsValid() => Id > 0;


    /* Comparison... */

    public bool Equals(EntityUid o) => Id == o.Id;
    public override bool Equals(object? o)
    {
        if (ReferenceEquals(null, o)) return false;
        return o is EntityUid id && Equals(id);
    }

    public override int GetHashCode()
    {
        /* 2^7-1 has no significance here. It just serves to ensure EntityUid's hash isn't confused with other things. */
        unchecked { return Id.GetHashCode() * 127; }
    }

    public static bool operator ==(EntityUid a, EntityUid b) => a.Id == b.Id;
    public static bool operator !=(EntityUid a, EntityUid b) => a.Id != b.Id;
    public static explicit operator long(EntityUid self) => self.Id;

    public int CompareTo(EntityUid o) => Id.CompareTo(o.Id);

    /* Miscellaneous... */
    public override string ToString() => Id.ToString();
}
