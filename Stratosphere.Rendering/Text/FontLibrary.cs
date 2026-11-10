namespace Stratosphere.Rendering.Text;

public interface IFontProvider { bool TryGetFont(string key, out IFontFace font); }

public sealed class FontLibrary : IFontProvider
{
    private readonly Dictionary<string, IFontFace> _fonts = new();
    public int Count => _fonts.Count;

    public bool TryGetFont(string key, out IFontFace font) => _fonts.TryGetValue(key, out font!);

    public T Add<T>(string key, T font) where T : IFontFace
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(font);
        _fonts[key] = font;
        return font;
    }

    public bool Remove(string key) => _fonts.Remove(key);
}
