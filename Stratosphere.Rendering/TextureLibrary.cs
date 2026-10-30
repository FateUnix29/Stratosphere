namespace Stratosphere.Rendering;

public sealed class TextureLibrary : ITextureProvider
{
    private readonly IRenderDevice _device;
    private readonly Dictionary<string, TextureInfo> _textures = new();

    public TextureLibrary(IRenderDevice device) { _device = device; }

    public int Count => _textures.Count;

    public bool TryGetTexture(string key, out TextureInfo info) => _textures.TryGetValue(key, out info);

    public TextureInfo Add(string key, int width, int height, ReadOnlySpan<byte> rgba8, TextureFilter filter = TextureFilter.Nearest)
    {
        if (rgba8.Length != width * height * 4)
            throw new ArgumentException($"'{key}' is {width}*{height}, which needs {width*height*4} bytes, but got {rgba8.Length} bytes.", nameof(rgba8));

        var handle = _device.CreateTexture(new TextureDescription(width, height, filter), rgba8);
        var info = new TextureInfo(handle, width, height);

        if (_textures.TryGetValue(key, out var old)) _device.DestroyTexture(old.Handle);

        _textures[key] = info;
        return info;
    }

    public bool Remove(string key)
    {
        if (!_textures.Remove(key, out var info)) return false;
        _device.DestroyTexture(info.Handle);
        return true;
    }
}
