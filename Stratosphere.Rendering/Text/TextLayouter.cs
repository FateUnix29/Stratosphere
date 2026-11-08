using System.Numerics;
using System.Text;

namespace Stratosphere.Rendering.Text;

public enum TextAlign : byte { Left, Center, Right }

public struct TextLayoutOptions
{
    /// <summary>Wrap lines wider than this many pixels. <= 0 will not wrap.</summary>
    public float MaxWidth;

    /// <summary>Where line sits within the block. With wrapping, the block is <see cref="MaxWidth"/> wide, otherwise, as long as the longest line. </summary>
    public TextAlign Align;

    /// <summary>Extra pixels between lines on top of the font's line height. Can be negative! That works. </summary>
    public float LineGap;
}

/// <summary>
/// A glyph that has to be drawn. The top left of its bitmap, and the atlas index that corresponds to this glyph.
/// As always, in pixels. X right, Y down.
/// </summary>
public readonly record struct PositionedGlyph(Vector2 Position, RectI Atlas);

public sealed class TextLayouter
{
    private enum Kind : byte { Glyph, Space, Newline }

    private struct Item
    {
        public Kind Kind;
        public GlyphInfo Glyph;
        public float Kern;
        public float Advance;
    }

    private struct Line
    {
        public int Start;
        public int End;
        public float Width;
    }

    private const int ReplacementChar = 0xFFFD;
    private const int TabWidthInSpaces = 4;

    private readonly List<Item> _items = new();
    private readonly List<Line> _lines = new();

    public Vector2 Layout(IFontFace font, ReadOnlySpan<char> text, in TextLayoutOptions options, List<PositionedGlyph>? output)
    {
        ArgumentNullException.ThrowIfNull(font);

        _items.Clear();
        _lines.Clear();

        Shape(font, text);
        if (_items.Count == 0) return Vector2.Zero;

        BreakLines(options.MaxWidth);

        float widest = 0f;
        foreach (var line in _lines) widest = MathF.Max(widest, line.Width);
        float blockWidth = options.MaxWidth > 0f ? MathF.Max(options.MaxWidth, widest) : widest;

        float step = font.LineHeight * options.LineGap;

        for (int l = 0; l < _lines.Count; l++)
        {
            var line = _lines[l];
            float baseline = l * step + font.Ascent;
            float x = options.Align switch
            {
                TextAlign.Center => MathF.Floor((blockWidth - line.Width) * 0.5f),
                TextAlign.Right => blockWidth - line.Width,
                _ => 0f
            };

            for (int k = line.Start; k < line.End; k++)
            {
                var item = _items[k];
                if (k > line.Start) x += item.Kern;

                if (output != null && item.Kind == Kind.Glyph && item.Glyph.IsVisible)
                    output.Add(new PositionedGlyph(new Vector2(x + item.Glyph.Offset.X, baseline+item.Glyph.Offset.Y), item.Glyph.Atlas));

                x += item.Advance;
            }
        }

        float height = _lines.Count * font.LineHeight + (_lines.Count - 1) * options.LineGap;
        return new Vector2(blockWidth, height);
    }

    private void Shape(IFontFace font, ReadOnlySpan<char> text)
    {
        float spaceAdvance = font.TryGetGlyph(' ', out var spaceGlyph) ? spaceGlyph.Advance : font.LineHeight * 0.3f;
        int previous = -1;

        foreach (Rune rune in text.EnumerateRunes())
        {
            int cp = rune.Value;
            if (cp == '\r') continue;
            if (cp == '\n')
            {
                _items.Add(new Item { Kind = Kind.Newline });
                previous = -1;
                continue;
            }

            if (cp == ' ' || cp == '\t')
            {
                _items.Add(new Item { Kind = Kind.Space, Advance = cp == '\t' ? spaceAdvance * TabWidthInSpaces : spaceAdvance });
                previous = -1;
                continue;
            }

            if (!TryGetGlyphOrFallback(font, cp, out var glyph, out int used)) continue;

            _items.Add(new Item
            {
                Kind = Kind.Glyph,
                Glyph = glyph,
                Kern = previous >= 0 ? font.GetKerning(previous, used) : 0f,
                Advance = glyph.Advance
            });

            previous = used;
        }
    }

    private static bool TryGetGlyphOrFallback(IFontFace font, int codepoint, out GlyphInfo glyph, out int used)
    {
        used = codepoint;
        if (font.TryGetGlyph(codepoint, out glyph)) return true;

        foreach (int fallback in stackalloc int[] { ReplacementChar, '?' })
        {
            if (fallback == codepoint) continue;
            used = fallback;
            if (font.TryGetGlyph(fallback, out glyph)) return true;
        }

        glyph = default;
        return false; /* font doesnt even have a question mark. the fuck do you want us to do? */
    }

    private void BreakLines(float maxWidth)
    {
        int n = _items.Count;
        int i = 0;
        bool wrapping = maxWidth > 0f;

        while (true)
        {
            int lineStart = i;
            float x = 0f;
            int contentEnd = lineStart;
            float contentWidth = 0f;
            bool newline = false;

            while (i < n)
            {
                if (_items[i].Kind == Kind.Newline) { newline = true; break; }

                bool spaces = _items[i].Kind == Kind.Space;
                int runStart = i;
                int j = i;
                float runWidth = 0f;

                while (j < n && _items[j].Kind != Kind.Newline && (_items[j].Kind == Kind.Space) == spaces)
                {
                    runWidth += (j == lineStart ? 0f : _items[j].Kern) + _items[j].Advance;
                    j++;
                }

                if (spaces)
                {
                    x += runWidth;
                    i = j;
                    continue;
                }

                if (wrapping && x + runWidth > maxWidth)
                {
                    bool hasContent = contentEnd > lineStart;
                    if (hasContent) break;

                    if (runWidth > maxWidth)
                    {
                        int k = runStart;
                        float w = x;
                        while (k < j)
                        {
                            float step = (k == lineStart ? 0f : _items[k].Kern) + _items[k].Advance;
                            if (w + step > maxWidth && k > runStart) break;
                            w += step;
                            k++;
                        }

                        contentEnd = k;
                        contentWidth = w;
                        x = w;
                        i = k;
                        if (k < j) break;
                        continue;
                    }

                }

                x += runWidth;
                contentEnd = j;
                contentWidth = x;
                i = j;
            }

            _lines.Add(new Line { Start = lineStart, End = contentEnd, Width = contentWidth });

            if (newline) { i++; continue; }
            if (i >= n) break;
        }
    }
}
