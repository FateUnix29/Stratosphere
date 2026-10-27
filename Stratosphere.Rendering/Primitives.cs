using System.Globalization;
using Stratosphere.Core.Serialization;

namespace Stratosphere.Rendering;

public readonly record struct Color(float R, float G, float B, float A = 1f) : IDataParsable<Color>
{
    public static readonly Color White = new(1,1,1);
    public static readonly Color Black = new(0,0,0);
    public static readonly Color Transparent = new(0,0,0,0);

    public static Color operator *(Color a, Color b) => new(a.R*b.R, a.G*b.G, a.B*b.B, a.A*b.A);

    public static Color FromHex(string hex)
    {
        var text = hex.Trim().TrimStart('#');
        if (text.Length is 3 or 4)
            text = string.Concat(text.Select(c => $"{c}{c}"));

        if (text.Length is not (6 or 8) || !text.All(Uri.IsHexDigit))
            throw new DataFormatException($"'{hex}' isn't a color. Expected a hex colorcode such as '#f4b630'.");

        float Channel(int index) => int.Parse(text.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
        return new Color(Channel(0), Channel(1), Channel(2), text.Length == 8 ? Channel(3) : 1f);
    }

    public static Color ParseData(object raw)
    {
        if (raw is string text)
        {
            return text.Trim().ToLowerInvariant() switch
            {
                "white" => White,
                "black" => Black,
                "transparent" => Transparent,
                "red" => new Color(1, 0, 0),
                "green" => new Color(0, 1, 0),
                "blue" => new Color(0, 0, 1),
                "yellow" => new Color(1, 1, 0),
                _ => FromHex(text)
            };
        }

        var n = DataParse.Numbers(raw, 3, 4, "a color", "[1, 0.5, 0, 1]");
        return new Color((float)n[0], (float)n[1], (float)n[2], n.Length > 3 ? (float)n[3] : 1f);
    }
}

/// <summary>
/// Rectangle measured in whole pixels. In YAML, <c>[x,y,width,height]</c>.
/// </summary>
public readonly record struct RectI(int X, int Y, int Width, int Height) : IDataParsable<RectI>
{
    public static RectI ParseData(object raw)
    {
        var n = DataParse.Numbers(raw, 4, 4, "a rectangle", "[x, y, width, height]");
        foreach (var value in n)
            if (value != Math.Floor(value)) throw new DataFormatException("Pixel rectangles need whole numbers.");
        return new RectI((int) n[0], (int) n[1], (int) n[2], (int) n[3]);
    }
}

/// <summary>
/// Rectangle measured in fractions. In YAML, (still) <c>[x,y,width,height]</c>.
/// </summary>
public readonly record struct RectF(float X, float Y, float Width, float Height) : IDataParsable<RectF>
{
    public static readonly RectF Unit = new(0, 0, 1, 1);

    public static RectF ParseData(object raw)
    {
        var n = DataParse.Numbers(raw, 4, 4, "a rectangle", "[x, y, width, height]");
        return new RectF((float) n[0], (float) n[1], (float) n[2], (float) n[3]);
    }
}

public readonly record struct SizeI(int Width, int Height);
