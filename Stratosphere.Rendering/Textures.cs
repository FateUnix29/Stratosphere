namespace Stratosphere.Rendering;

public enum TextureFilter : byte
{
    /// <summary>
    /// Crisp, blocky pixels when scaled, for pixel art.
    /// </summary>
    Nearest,
    Linear
}

public readonly record struct TextureDescription(int Width, int Height, TextureFilter Filter = TextureFilter.Nearest);
public readonly record struct RenderTargetDescription(int Width, int Height, TextureFilter Filer = TextureFilter.Linear);

public readonly record struct TextureInfo(TextureHandle Handle, int Width, int Height)
{ public bool IsValid => Handle.IsValid; }

public interface ITextureProvider
{ bool TryGetTexture(string key, out TextureInfo info); }
