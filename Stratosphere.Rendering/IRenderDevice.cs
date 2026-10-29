namespace Stratosphere.Rendering;


/// <summary>
/// The rest of the engine uses this to draw... Safe to call from the game's thread, whatever thread the graphics API lives on.
/// </summary>
public interface IRenderDevice : IDisposable
{
    TextureHandle CreateTexture(in TextureDescription description, ReadOnlySpan<byte> rgba8);
    void DestroyTexture(TextureHandle handle);

    RenderTargetHandle CreateRenderTarget(in RenderTargetDescription description, out TextureHandle color);
    void DestroyRenderTarget(RenderTargetHandle target);

    SizeI GetTargetSize(RenderTargetHandle handle);

    RenderFrame AcquireFrame();
    void Submit(RenderFrame frame);
}
