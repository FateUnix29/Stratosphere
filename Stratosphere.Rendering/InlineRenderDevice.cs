namespace Stratosphere.Rendering;

public sealed class InlineRenderDevice : IRenderDevice
{
    private readonly IRenderBackend _backend;
    private readonly HandleAllocator _handles = new();
    private readonly RenderFrame _frame = new();

    public InlineRenderDevice(IRenderBackend backend) { _backend = backend; }

    public TextureHandle CreateTexture(in TextureDescription description, ReadOnlySpan<byte> rgba8)
    {
        var handle = _handles.NewTexture();
        _backend.CreateTexture(handle, in description, rgba8);
        return handle;
    }

    public void DestroyTexture(TextureHandle texture) => _backend.DestroyTexture(texture);

    public RenderTargetHandle CreateRenderTarget(in RenderTargetDescription description, out TextureHandle color)
    {
        color = _handles.NewTexture();
        var handle = _handles.NewRenderTarget(new SizeI(description.Width, description.Height));
        _backend.CreateRenderTarget(handle, color, in description);
        return handle;
    }

    public void DestroyRenderTarget(RenderTargetHandle target)
    {
        _backend.DestroyRenderTarget(target);
        _handles.ForgetRenderTarget(target);
    }

    public SizeI GetTargetSize(RenderTargetHandle target)
    {
        if (target.IsScreen) return _backend.ScreenSize;
        return _handles.TryGetTargetSize(target, out var size) ? size : throw new ArgumentException($"{target} is not a render target that actually exists.", nameof(target));
    }

    public RenderFrame AcquireFrame()
    {
        _frame.Clear();
        return _frame;
    }

    public void Submit(RenderFrame frame) => _backend.Render(frame);
    public void Dispose() => _backend.Dispose();
}
