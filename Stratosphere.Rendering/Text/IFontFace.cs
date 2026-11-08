using System.Numerics;

namespace Stratosphere.Rendering.Text;

/// <summary>
/// One glyph of a font at a single size, as how it sits within the font's atlas. Everything is, of course, measured in pixels.
/// </summary>
public readonly record struct GlyphInfo(float Advance, Vector2 Offset, RectI Atlas)
{ public bool IsVisible => Atlas.Width > 0 && Atlas.Height > 0; }

/// <summary>
/// A font at a specific size (in pixels), ready to be drawn from.
/// A bridge between the layout code, and whatever made the glyphs, such as a TTF rasterizer, or a hand-drawn pixel font, etc.
/// Therefore, a layout should never need to know where glyphs come from! Much like how our rendering works.
/// </summary>
public interface IFontFace
{
    float LineHeight { get; }

    /// <summary>Pixels from top-of-line to baseline.</summary>
    float Ascent { get; }

    /// <summary>Texture holding every glyph.</summary>
    TextureInfo Atlas { get; }

    bool TryGetGlyph(int codepoint, out GlyphInfo glyph);

    /// <summary>
    /// Extra pixels (usually negative, or zero) between two glyphs that sit right next to each other. Zero if the font has no opinion on it.
    /// </summary>
    float GetKerning(int left, int right);
}
