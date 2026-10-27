using System.Numerics;
using Stratosphere.Core.Maths;

namespace Stratosphere.Rendering;

/// <summary>
/// What things a view is meant to draw.
/// A sprite, for example, is drawn by a view only if they share at least one bit.
/// This is how one single world feeds several different unrelated pictures, such as camera, UI, etc.
/// </summary>
[Flags]
public enum RenderMask : uint
{
    None = 0,
    World = 1 << 0,
    UI = 1 << 1,
    Custom0 = 1 << 2,
    Custom1 = 1 << 3,
    Custom2 = 1 << 4,
    Custom3 = 1 << 5,
    All = uint.MaxValue
}

/// <summary>
/// How things overlapping at the same draw-depth are ordered/prioritized.
/// </summary>
public enum SortMode : byte
{
    /// <summary>
    /// Things further away are prioritized and drawn first, which means things closer will be drawn over things further. Exactly what's wanted for 3D and 2D with distinct depths!
    /// </summary>
    Distance,
    /// <summary>
    /// Higher up on the screen is drawn first, things lower on the screen are drawn over things higher than them. Exactly what's wanted for a typical top-down 2D game!
    /// </summary>
    ViewY
}

public struct RenderView
{   
    /// <summary>
    /// The world position that the view's draw list is relative to. Backends won't need this, it's mostly debugging and tools.
    /// </summary>
    public Vector3d Origin;

    /// <summary>
    /// The direction of the camera in the world.
    /// </summary>
    public Quaternion Rotation;

    /// <summary>
    /// Matrix used for taking a position relative to an origin into the camera's own space (who looks in the -Z direction). Rotation only.
    /// </summary>
    public Matrix4x4 ViewMatrix;

    public Matrix4x4 ProjectionMatrix;

    /// <summary>
    /// Area of target to draw into, in pixels from the top-left.
    /// </summary>
    public RectI Viewport;

    public RenderTargetHandle Target;

    public RenderMask Mask;
    public SortMode Sort;

    /// <summary>
    /// Clear the viewport with ClearColor before drawing?
    /// </summary>
    public bool Clear;
    public Color ClearColor;

    public readonly Matrix4x4 ViewProjection => ViewMatrix * ProjectionMatrix;
}

