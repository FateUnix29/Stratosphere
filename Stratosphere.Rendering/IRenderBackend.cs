namespace Stratosphere.Rendering;

/// <summary>
/// What the engine sees of a graphics API such as OpenGL, Vulkan, recorder for testing, terminal, anything that implements this.
/// A bridge between them, allowing the engine to be entirely backend-agnostic.
/// </summary>
/// <remarks>
/// Deliberately small. Creates things, draws a finished RenderFrame, that's about it.
/// It doesn't really decide what's in that frame. That's the other engine code's job.
/// 
/// Backends are expected to obey specific conventions...
/// - Positions are right-handed; -X/+X left/right, -Y/+Y up/down, +Z/-Z backward/forward (so camera looks down -Z).
/// - Clip-space depth is 0 (near) - 1 (far), as in Vulkan and Direct3D. OpenGL backends and the sort will need glClipControl or some kind of adjustment to the projection.
/// - Pixel coordinates (viewports, texture regions, ...) start top-left, goes right and down. OpenGL is upside-down by this logic, so need to account for that.
/// </remarks>
public interface IRenderBackend : IDisposable
{
    TextureHandle CreateTexture(in TextureDescription description, ReadOnlySpan<byte> rgba8);
    void DestroyHandle(TextureHandle texture);

    RenderTargetHandle CreateRenderTarget(in RenderTargetDescription description, out TextureHandle color);
    void DestroyRenderTarget(RenderTargetHandle target);

    SizeI GetTargetSize(RenderTargetHandle target);

    void Render(RenderFrame frame);
}
