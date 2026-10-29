namespace Stratosphere.Rendering;

internal sealed class HandleAllocator
{
    private uint _next;
    private readonly Dictionary<RenderTargetHandle, SizeI> _targetSizes = new();

    private uint Next()
    {
        uint value = Interlocked.Increment(ref _next);
        if (value == 0) throw new InvalidOperationException($"Ran out of render handles... What the fuck did you do.");
        return value;
    }

    public TextureHandle NewTexture() => new(Next());

    public RenderTargetHandle NewRenderTarget(SizeI size)
    {
        var handle = new RenderTargetHandle(Next());
        lock (_targetSizes) _targetSizes[handle] = size;
        return handle;
    }

    public void ForgetRenderTarget(RenderTargetHandle handle)
    { lock (_targetSizes) _targetSizes.Remove(handle); }

    public bool TryGetTargetSize(RenderTargetHandle handle, out SizeI size)
    { lock (_targetSizes) return _targetSizes.TryGetValue(handle, out size); }
}
