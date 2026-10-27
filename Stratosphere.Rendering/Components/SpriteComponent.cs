using System.Numerics;
using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Rendering.Components;

public sealed class SpriteLayer
{
    private string? _texture;
    internal TextureInfo _info;
    internal bool _resolved;

    [DataField("texture")]
    public string? Texture
    {
        get => _texture;
        set { _texture = value; _resolved = false; _info = default; }
    }

    [DataField("region")] public RectI? Region;
    [DataField("size")] public Vector2? Size;
    [DataField("offset")] public Vector2 Offset;
    [DataField("scale")] public Vector2 Scale = Vector2.One;
    [DataField("rot")] public float RotationDegrees;
    [DataField("color")] public Color Color = Color.White;
    [DataField("visible")] public bool Visible = true;

    public void Bind(TextureInfo info)
    {
        _texture = null;
        _info = info;
        _resolved = true;
    }

    public SpriteLayer Clone() => (SpriteLayer)MemberwiseClone();
}

[RegisterComponent]
public sealed partial class SpriteComponent : Component
{
    [DataField("layers")] public List<SpriteLayer> Layers = new();
    [DataField("visible")] public bool Visible = true;

    /// <summary>
    /// Multiplies every layer by this color.
    /// </summary>
    [DataField("color")] public Color Color = Color.White;

    /// <summary>
    /// Scales entire sprite, including where the layers sit.
    /// </summary>
    [DataField("scale")] public Vector2 Scale = Vector2.One;

    /// <summary>
    /// Coarse ordering. Lower numbers are drawn first/behind.
    /// </summary>
    [DataField("drawDepth")] public int DrawDepth;

    /// <summary>
    /// Which view draws this.
    /// </summary>
    [DataField("mask")] public RenderMask Mask = RenderMask.World;

    /// <summary>
    /// How many pixels of texture adds up to one world unit. Bigger translates to being smaller on screen.
    /// For example, a 32x32-per-tile tile-based-game is going to want 32 here, as that would mean the sprite is 1 tile big.
    /// Something laid out in screen pixels definitely wants 1, like an interface.
    /// </summary>
    [DataField("pixelsPerUnit")] public float PixelsPerUnit = 32f;

    /// <summary>
    /// Always face the camera, ignore rotation.
    /// </summary>
    [DataField("billboard")] public bool Billboard;

    public override IComponent Clone()
    {
        var copy = (SpriteComponent)base.Clone();
        copy.Layers = new List<SpriteLayer>(Layers.Count);
        foreach (var layer in Layers) copy.Layers.Add(layer.Clone());
        return copy;
    }
}
