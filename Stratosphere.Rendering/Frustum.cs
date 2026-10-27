using System.Numerics;

namespace Stratosphere.Rendering;

/// <summary>
/// The volume a camera can see, as six planes. Used for frustum culling; Throwing away things that we cannot possibly see.
/// </summary>
/// <remarks>
/// Built from a view-projection matrix, so it should theoretically work the same for both orthographic and perspective cameras with no special cases.
/// That being said, for equirectangular, it's a different story...
/// </remarks>
public readonly struct Frustum
{
    private readonly Plane _left, _right, _bottom, _top, _near, _far;

    private Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
    { _left = left; _right = right; _bottom = bottom; _top = top; _near = near; _far = far; }

    /// <summary>
    /// Point is inside when -w <= x <= w, -w <= y <= w, and 0 <= z <= w, after the matrix (0-1 depth range).
    /// </summary>
    public static Frustum FromMatrix(in Matrix4x4 m)
    {
        static Plane Make(float a, float b, float c, float d)
        {
            var length = MathF.Sqrt(a * a + b * b + c * c);
            return new Plane(a / length, b / length, c / length, d / length);
        }

        return new Frustum(
            left:   Make(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41),
            right:  Make(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41),
            bottom: Make(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42),
            top:    Make(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42),
            near:   Make(m.M13,         m.M23,         m.M33,         m.M43),
            far:    Make(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43));
    }

    /// <summary>
    /// False only if the sphere is 100% outside. A little generous near corners.
    /// </summary>
    public bool IntersectsSphere(Vector3 center, float radius)
        => Plane.DotCoordinate(_left, center) >= -radius
        && Plane.DotCoordinate(_right, center) >= -radius
        && Plane.DotCoordinate(_bottom, center) >= -radius
        && Plane.DotCoordinate(_top, center) >= -radius
        && Plane.DotCoordinate(_near, center) >= -radius
        && Plane.DotCoordinate(_far, center) >= -radius;
}
