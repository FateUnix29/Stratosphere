using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Rendering.Components;

public enum CameraProjection : byte
{
    /// <summary>Isometric. FOV is irrelevant, flat and 2D, things stay the same size (no perspective).</summary>
    Orthographic,
    /// <summary>Perspective. Things shrink with distance. FOV is relevant.</summary>
    Perspective,
    /// <summary>Recommendation: Do not fucking touch this one.</summary>
    Equirectangular /* For implementing things similar to the SS13/SS14 Station AI */
}

/// <summary>
/// Makes an entity an 'eye,' something the render systems produce an image for.
/// The render systems produce a frame for every enabled camera.
/// </summary>
/// <remarks>
/// Looks down its own -Z at zero rotation. Which also means +X is to the right, -X to the left. +Y is up and -Y is down.
/// To get a top-down perspective, pitch it downward 90 degrees and use orthographic.
/// </remarks>
[RegisterComponent]
public sealed partial class CameraComponent : Component
{
    [DataField("enabled")] public bool Enabled = true;
    [DataField("projection")] public CameraProjection Projection = CameraProjection.Perspective;
    [DataField("orthoSize")] public float OrthographicSize = 15f;
    [DataField("fov")] public float FieldOfViewDegrees = 70f;
    [DataField("near")] public float Near = 0.1f;
    [DataField("far")] public float Far = 1000f;
    [DataField("viewport")] public RectF Viewport = RectF.Unit;
    [DataField("mask")] public RenderMask Mask = RenderMask.World;
    [DataField("sort")] public SortMode Sort = SortMode.Distance;
    [DataField("order")] public int Order;
    [DataField("clear")] public bool Clear = true;
    [DataField("clearColor")] public Color ClearColor = Color.Black;

    public RenderTargetHandle Target = RenderTargetHandle.Screen;
}
