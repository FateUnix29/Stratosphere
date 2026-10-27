using System.Numerics;
using System.Runtime.CompilerServices;

namespace Stratosphere.Core.Maths;

public readonly struct Vector3d : IEquatable<Vector3d>
{
    public readonly double X, Y, Z;

    public static readonly Vector3d Zero = default;

    public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
    public Vector3d(double x, double y) { X = x; Y = y; Z = 0; }

    /* Floats can widen to doubles just fine, so this can be implicit. */
    public static implicit operator Vector3d(Vector3 v) => new(v.X, v.Y, v.Z);

    /* Narrows it down to floats. Very dangerous. Small numbers only, whether that be from origin or relative to something. */
    public Vector3 ToVector3() => new((float) X, (float) Y, (float) Z);

    /// <summary>
    /// This position as an offset from another, narrowed down to floats. The actual math still happens in double precision, though.
    /// The idea is that the difference between two huge doubles will still be small enough for a float to be accurate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 RelativeTo(Vector3d origin) => new((float) (X - origin.X), (float) (Y - origin.Y), (float) (Z - origin.Z));

    public double MagnitudeSquared => X * X + Y * Y + Z * Z;
    public double Magnitude => Math.Sqrt(MagnitudeSquared);

    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3d Cross(Vector3d a, Vector3d b)
        => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /* QUATERNION!!! */
    public static Vector3d Rotate(Quaternion q, Vector3d v)
    {
        double qx = q.X, qy = q.Y, qz = q.Z, qw = q.W;

        /* t = 2 * cross(q.xyz, v);  v' = v + w * t + cross(q.xyz, t) */
        double tx = 2 * (qy * v.Z - qz * v.Y);
        double ty = 2 * (qz * v.X - qx * v.Z);
        double tz = 2 * (qx * v.Y - qy * v.X);

        return new Vector3d(
            v.X + qw * tx + (qy * tz - qz * ty),
            v.Y + qw * ty + (qz * tx - qx * tz),
            v.Z + qw * tz + (qx * ty - qy * tx));
    }

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(double s, Vector3d a) => a * s;
    public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public static bool operator ==(Vector3d a, Vector3d b) => a.Equals(b);
    public static bool operator !=(Vector3d a, Vector3d b) => !a.Equals(b);

    public bool Equals(Vector3d o) => X == o.X && Y == o.Y && Z == o.Z;
    public override bool Equals(object? o) => o is Vector3d v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";
}
