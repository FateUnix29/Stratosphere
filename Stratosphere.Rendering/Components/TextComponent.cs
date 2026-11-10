using System.Numerics;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Rendering.Text;

namespace Stratosphere.Rendering.Components;

[RegisterComponent]
public sealed partial class TextComponent : Component
{
    private string _text = "";
    private string? _font;
    private float _maxWidth;
    private TextAlign _align;
    private float _lineGap;

    internal List<PositionedGlyph> Glyphs = new();
    internal Vector2 LaidOutSize;
    internal IFontFace? LaidOutWith;
    internal bool Dirty = true;


    public string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (_text == value) return;
            _text = value;
            Dirty = true;
        }
    }

    public string? Font
    {
        get => _font;
        set
        {
            if (_font == value) return;
            _font = value;
            Dirty = true;
        }
    }

    public float MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (_maxWidth == value) return;
            _maxWidth = value;
            Dirty = true;
        }
    }

    public TextAlign Align
    {
        get => _align;
        set
        {
            if (_align == value) return;
            _align = value;
            Dirty = true;
        }
    }

    public float LineGap
    {
        get => _lineGap;
        set
        {
            if (_lineGap == value) return;
            _lineGap = value;
            Dirty = true;
        }
    }

    public Vector2 Pivot = new(0.5f,0.5f);
    public Vector2 Scale = Vector2.One;
    public Color Color = Color.White;
    public float PixelsPerUnit = 32f;
    public int DrawDepth;
    public RenderMask Mask = RenderMask.World;
    public bool Billboard;
    public bool Visible = true;

    public override IComponent Clone()
    {
        var copy = (TextComponent)base.Clone();
        copy.Glyphs = new List<PositionedGlyph>();
        copy.LaidOutWith = null;
        copy.LaidOutSize = Vector2.Zero;
        copy.Dirty = true;
        return copy;
    }
}
