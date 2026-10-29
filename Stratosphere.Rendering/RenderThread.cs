using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Stratosphere.Core.Logging;

namespace Stratosphere.Rendering;

public sealed class RenderThread : IRenderDevice
{
    private abstract class Work { }
    private sealed class CreateTextureWork(TextureHandle handle, TextureDescription description, byte[] pixels, int length) : Work
    { public readonly TextureHandle Handle = handle; public readonly TextureDescription Description = description; public readonly byte[] Pixels = pixels; public readonly int Length = length; }
    private sealed class DestroyTextureWork(TextureHandle handle) : Work { public readonly TextureHandle Handle = handle; }
    private sealed class CreateTargetWork(RenderTargetHandle handle, TextureHandle color, RenderTargetDescription description) : Work
    { public readonly RenderTargetHandle Handle = handle; public readonly TextureHandle Color = color; public readonly RenderTargetDescription Description = description; }
    private sealed class DestroyTargetWork(RenderTargetHandle handle) : Work { public readonly RenderTargetHandle Handle = handle; }
    private sealed class FrameWork(RenderFrame frame) : Work { public readonly RenderFrame Frame = frame; }
    private sealed class SyncWork : Work { public readonly ManualResetEventSlim Done = new(); }
    private sealed class StopWork : Work { }

    private readonly BlockingCollection<Work> _queue = new(new ConcurrentQueue<Work>());
    private readonly HandleAllocator _handles = new();
    private readonly Thread _thread;
    private readonly Sawmill? _log;

    private readonly ConcurrentBag<RenderFrame> _freeFrames = new();
    private readonly SemaphoreSlim _frameSlots;

    private long _screenSize;
    private ExceptionDispatchInfo? _fault;
    private bool _disposed;

    public RenderThread(Func<IRenderBackend> createBackend, string name = "Render", int framesInFlight = 2, Sawmill? log = null)
    {
        if (framesInFlight < 1) throw new ArgumentException(nameof(framesInFlight));

        _log = log;
        _frameSlots = new SemaphoreSlim(framesInFlight, framesInFlight);
        for (int i = 0; i < framesInFlight; i++) _freeFrames.Add(new RenderFrame());

        var started = new ManualResetEventSlim();
        Exception? startFailure = null;

        _thread = new Thread(() => Run(createBackend, started, e => startFailure = e)) { Name = name, IsBackground = true };
        _thread.Start();
        started.Wait();

        if (startFailure != null)
        {
            _thread.Join();
            ExceptionDispatchInfo.Capture(startFailure).Throw();
        }
    }

    private void Run(Func<IRenderBackend> createBackend, ManualResetEventSlim started, Action<Exception> failed)
    {
        IRenderBackend backend;
        try
        {
            backend = createBackend();
            PublishScreenSize(backend.ScreenSize);
        }
        catch (Exception e)
        {
            failed(e);
            started.Set();
            return;
        }

        started.Set();

        try
        {
            while (true)
            {
                if (!_queue.TryTake(out var work, 16))
                {
                    backend.Pump();
                    continue;
                }

                if (work is StopWork) break;
                Perform(backend, work);
            }
        }
        catch (Exception e)
        {
            _fault = ExceptionDispatchInfo.Capture(e);
            _log?.Error(e, "The render thread crashed. Ugh.");
            DrainAndRelease();
        }
        finally
        {
            try { backend.Dispose(); }
            catch (Exception e) { _log?.Error(e, "Backend threw while shutting down. Oh... Fuck off."); }
        }
    }

    private void DrainAndRelease()
    {
        while (_queue.TryTake(out var work))
            if (work is SyncWork sync) sync.Done.Set();
    }

    private void Perform(IRenderBackend backend, Work work)
    {
        switch (work)
        {
            case CreateTextureWork create:
                try { backend.CreateTexture(create.Handle, create.Description, new ReadOnlySpan<byte>(create.Pixels, 0, create.Length)); }
                finally { ArrayPool<byte>.Shared.Return(create.Pixels); }
                break;
            case DestroyTextureWork destroy: backend.DestroyTexture(destroy.Handle); break;
            case CreateTargetWork create: backend.CreateRenderTarget(create.Handle, create.Color, create.Description); break;
            case DestroyTargetWork destroy: backend.DestroyRenderTarget(destroy.Handle); break;
            case FrameWork frame:
                try
                {
                    backend.Render(frame.Frame);
                    PublishScreenSize(backend.ScreenSize);
                }
                finally
                {
                    _freeFrames.Add(frame.Frame);
                    _frameSlots.Release();
                }
                break;
            case SyncWork sync: sync.Done.Set(); break;
        }
    }

    private void PublishScreenSize(SizeI size) => Volatile.Write(ref _screenSize, ((long) size.Width << 32) | (uint) size.Height);

    private void ThrowIfFaulted()
    {
        if (_fault != null)
            throw new InvalidOperationException("The render thread crashed and isn't drawing.", _fault.SourceException);
        if (_disposed) throw new ObjectDisposedException(nameof(RenderThread));
    }

    private void Enqueue(Work work)
    {
        ThrowIfFaulted();
        _queue.Add(work);
    }

    public TextureHandle CreateTexture(in TextureDescription description, ReadOnlySpan<byte> rgba8)
    {
        var handle = _handles.NewTexture();
        var copy = ArrayPool<byte>.Shared.Rent(rgba8.Length);
        rgba8.CopyTo(copy);
        Enqueue(new CreateTextureWork(handle, description, copy, rgba8.Length));
        return handle;
    }

    public void DestroyTexture(TextureHandle texture) => Enqueue(new DestroyTextureWork(texture));

    public RenderTargetHandle CreateRenderTarget(in RenderTargetDescription description, out TextureHandle color)
    {
        color = _handles.NewTexture();
        var handle = _handles.NewRenderTarget(new SizeI(description.Width, description.Height));
        Enqueue(new CreateTargetWork(handle, color, description));
        return handle;
    }

    public void DestroyRenderTarget(RenderTargetHandle target)
    {
        Enqueue(new DestroyTargetWork(target));
        _handles.ForgetRenderTarget(target);
    }

    public SizeI GetTargetSize(RenderTargetHandle target)
    {
        if (target.IsScreen)
        {
            long packed = Volatile.Read(ref _screenSize);
            return new SizeI((int) (packed >> 32), (int) (packed & 0xFFFFFFFF));
        }

        return _handles.TryGetTargetSize(target, out var size) ? size : throw new ArgumentException($"{target} is not a render target that actually exists.", nameof(target));
    }

    public RenderFrame AcquireFrame()
    {
        ThrowIfFaulted();

        while (!_frameSlots.Wait(50)) ThrowIfFaulted();
        ThrowIfFaulted();

        if (!_freeFrames.TryTake(out var frame)) frame = new RenderFrame();
        frame.Clear();
        return frame;
    }

    public void Submit(RenderFrame frame) => Enqueue(new FrameWork(frame));

    public void Flush()
    {
        var sync = new SyncWork();
        Enqueue(sync);
        sync.Done.Wait();
        ThrowIfFaulted();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_fault == null) _queue.Add(new StopWork());
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}
