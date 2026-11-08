using System.Numerics;
using SharpFont;

namespace Stratosphere.Rendering.Text;

public sealed class BakedFont
{
    private const int Padding = 1;
    private static readonly int[] AtlasWidths = { 256, 512, 1024, 2048, 4096 };

    private struct RawGlyph
    {
        public int Codepoint;
        public uint GlyphIndex;
        public float Advance;
        public int Left, Top, Width, Height;
        public byte[] Coverage;
    }

    private readonly TtfFont _font;
    private readonly bool _snap;
    private readonly Dictionary<int, GlyphInfo> _glyphs;
    private readonly Dictionary<int, uint> _glyphIndices;
    private readonly Dictionary<(int, int), float> _kerning = new();

    public float LineHeight { get; }
    public float Ascent { get; }
    public TextureInfo Atlas { get; }

    public string Key { get; }

    public float PixelSize { get; }

    public int GlyphCount => _glyphs.Count;

    private BakedFont(TtfFont font, string key, float pixelSize, bool snap, float lineHeight, float ascent,
                      TextureInfo atlas, Dictionary<int, GlyphInfo> glyphs, Dictionary<int, uint> glyphIndices)
    {
        _font = font; Key = key; PixelSize = pixelSize; _snap = snap;
        LineHeight = lineHeight; Ascent = ascent; Atlas = atlas;
        _glyphs = glyphs; _glyphIndices = glyphIndices;
    }

    public static BakedFont Bake(TextureLibrary textures, string key, TtfFont font, float pixelSize, IEnumerable<int>? codepoints = null,
                                 TextureFilter filter = TextureFilter.Linear, bool snapAdvances = true)
    {
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(font);
        if (!(pixelSize >0f)) throw new ArgumentOutOfRangeException(nameof(pixelSize), pixelSize, "Font has to be some number of pixels tall.");

        var face = font.Face;

        try { face.SetCharSize(new Fixed26Dot6(pixelSize), new Fixed26Dot6(pixelSize), 72, 72); }
        catch (FreeTypeException e) { throw new InvalidOperationException($"'{font.Name}' can't be drawn at {pixelSize}px ({e.Error}). Fixed-size bitmap fonts aren't supported.",e); }

        var metrics = face.Size.Metrics;
        var ascentPx = metrics.Ascender.ToSingle();
        float lineHeightPx = metrics.Height.ToSingle();

        if (snapAdvances)
        {
            ascentPx = MathF.Round(ascentPx);
            lineHeightPx = MathF.Round(lineHeightPx);
        }

        var flags = snapAdvances ? LoadFlags.Default : LoadFlags.NoHinting;
        var raw = new List<RawGlyph>();
        var seen = new HashSet<int>();

        foreach (int cpt in codepoints ?? Charsets.Latin1())
        {
            if (!seen.Add(cpt) || cpt < 0) continue;

            uint glyphIndex = face.GetCharIndex((uint)cpt);
            if (glyphIndex == 0) continue;

            face.LoadGlyph(glyphIndex, flags, LoadTarget.Normal);
            var slot = face.Glyph;
            slot.RenderGlyph(RenderMode.Normal);

            var bitmap = slot.Bitmap;
            int width = bitmap.Width;
            int height = bitmap.Rows;
            byte[] coverage = Array.Empty<byte>();

            if (width > 0 && height > 0)
            {
                if (bitmap.PixelMode != PixelMode.Gray)
                    throw new InvalidOperationException($"'{font.Name}' gave {bitmap.PixelMode} bitmap for U+{cpt:X4}, only plain grayscale is understood.");

                coverage = CopyRows(bitmap.Buffer, bitmap.Pitch, width, height);
            }
            else width = height = 0;
        
            float advancePx = slot.Advance.X.ToSingle();
            if (snapAdvances) advancePx = MathF.Round(advancePx);

            raw.Add(new RawGlyph { Codepoint = cpt, GlyphIndex = glyphIndex, Advance = advancePx, Left = slot.BitmapLeft, Top = slot.BitmapTop, Width = width, Height = height });
        }

        var visible = new List<int>();
        for (int i = 0; i < raw.Count; i++) if (raw[i].Width > 0) visible.Add(i);

        var sizes = visible.Select(i => new SizeI(raw[i].Width, raw[i].Height)).ToArray();
        var positions = new (int X, int Y)[sizes.Length];

        int atlasWidth = 1, atlasHeight = 1;
        if (sizes.Length > 0)
        {
            bool packed = false;
            foreach (int width in AtlasWidths)
            {
                if (!ShelfPacker.TryPack(sizes, width, Padding, width, positions, out int usedHeight)) continue;
                atlasWidth = width; atlasHeight = usedHeight; packed = true; break;
            }

            if (!packed)
                throw new InvalidOperationException($"{sizes.Length} glyphs of '{font.Name}' at {pixelSize}px won't fit in {AtlasWidths[^1]}*{AtlasWidths[^1]} atlas, bake fewer characters or smaller size.");
        }

        var rgba = new byte[atlasWidth * atlasHeight * 4];
        Array.Fill(rgba, (byte)255);
        for (int i = 0; i < rgba.Length; i++) rgba[i] = 0;

        var glyphs = new Dictionary<int, GlyphInfo>(raw.Count);
        var glyphIndices = new Dictionary<int, uint>(raw.Count);
        var slotOf = new Dictionary<int, int>(visible.Count);
        for (int s = 0; s < visible.Count; s++) slotOf[visible[s]] = s;

        for (int i = 0; i < raw.Count; i++)
        {
            var g = raw[i];
            var atlasRect = default(RectI);

            if (g.Width > 0)
            {
                var (px, py) = positions[slotOf[i]];
                atlasRect = new RectI(px, py, g.Width, g.Height);

                for (int row = 0; row < g.Height; row++)
                    for (int col = 0; col < g.Width; col++)
                        rgba[((py + row) * atlasWidth + px + col) * 4 + 3] = g.Coverage[row*g.Width + col];
            }

            glyphs[g.Codepoint] = new GlyphInfo(g.Advance, new Vector2(g.Left, -g.Top), atlasRect);
            glyphIndices[g.Codepoint] = g.GlyphIndex;
        }

        var atlas = textures.Add(key, atlasWidth, atlasHeight, rgba, filter);
        return new BakedFont(font, key, pixelSize, snapAdvances, lineHeightPx, ascentPx, atlas, glyphs, glyphIndices);
    }

    private static byte[] CopyRows(IntPtr buffer, int pitch, int width, int height)
    {
        var result = new byte[width * height];

        /* Pitch is step from one row to next, can be negative. Never assume it equals width. */
        for (int row = 0; row < height; row++)
            System.Runtime.InteropServices.Marshal.Copy(buffer + row * pitch, result, row * width, width);

        return result;
    }

    public bool TryGetGlyph(int codepoint, out GlyphInfo glyph) => _glyphs.TryGetValue(codepoint, out glyph);

    public float GetKerning(int left, int right)
    {
        if (!_glyphIndices.TryGetValue(left, out uint leftIndex) || !_glyphIndices.TryGetValue(right, out uint rightIndex)) return 0f;
        if (_kerning.TryGetValue((left, right), out float cached)) return cached;

        float kern = 0f;
        var face = _font.Face;

        if (face.HasKerning)
        {
            face.SetCharSize(new Fixed26Dot6(PixelSize), new Fixed26Dot6(PixelSize), 72, 72);
            kern = face.GetKerning(leftIndex, rightIndex, _snap ? KerningMode.Default : KerningMode.Unfitted).X.ToSingle();
            if (_snap) kern = MathF.Round(kern);
        }

        _kerning[(left, right)] = kern;
        return kern;
    }
}
