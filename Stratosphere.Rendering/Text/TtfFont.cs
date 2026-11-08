using SharpFont;

namespace Stratosphere.Rendering.Text;

public sealed class TtfFont : IDisposable
{
    private readonly Face _face;
    private bool _disposed;

    public string Name { get; }

    private TtfFont(Face face, string name) { _face = face; Name = name; }

    internal Face Face
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _face;
        }
    }

    public string? FamilyName => Face.FamilyName;
    public static TtfFont FromFile(string path, int index = 0) => FromBytes(File.ReadAllBytes(path), path, index);
    public static TtfFont FromBytes(byte[] data, string name = "font data", int index = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (data.Length < 12) throw new InvalidDataException($"'{name}' is {data.Length} bytes, which is far too small to be a font");

        var library = FreeTypeLibrary.Instance;

        try { return new TtfFont(new Face(library, data, index), name); }
        catch (FreeTypeException e) { throw new InvalidDataException($"'{name}' isn't a font readable at index {index} ({e.Error}).", e); }
    }

    public bool HasGlyph(int codepoint) => codepoint >= 0 && Face.GetCharIndex((uint)codepoint) != 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _face.Dispose();
    }
}
