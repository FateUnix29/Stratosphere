using System.Threading.Channels;

namespace Stratosphere.Core.Logging;

public sealed class LogManager : IDisposable
{
    private readonly object _config = new();
    private readonly Dictionary<string, Sawmill> _sawmills = new();
    private readonly Dictionary<string, LogLevel> _levels = new(StringComparer.Ordinal);
    private ILogSink[] _sinks = Array.Empty<ILogSink>();
    private int _levelVersion;
    private int _defaultLevel = (int) LogLevel.Info;

    private readonly bool _async;
    private readonly object _syncWrite = new();
    private Channel<Item>? _queue;
    private Thread? _writer;
    private bool _disposed;

    private readonly struct Item
    {
        public readonly LogEntry Entry;
        public readonly ManualResetEventSlim? FlushDone;
        public Item(LogEntry entry) { Entry = entry; FlushDone = null; }
        public Item(ManualResetEventSlim flushDone) { Entry = default; FlushDone = flushDone; }
    }

    public LogManager(bool async = true) { _async = async; }

    public LogLevel DefaultLevel
    {
        get => (LogLevel)Volatile.Read(ref _defaultLevel);
        set { Volatile.Write(ref _defaultLevel, (int)value); Interlocked.Increment(ref _levelVersion); }
    }

    internal int LevelVersion => Volatile.Read(ref _levelVersion);
    internal bool HasSinks => _sinks.Length > 0;

    public void SetLevel(string channel, LogLevel level)
    {
        lock (_config) _levels[channel] = level;
        Interlocked.Increment(ref _levelVersion);
    }

    public void AddSink(ILogSink sink)
    {
        lock (_config) _sinks = [.._sinks, sink];
    }

    public Sawmill GetSawmill(string name)
    {
        lock (_config)
        {
            if (!_sawmills.TryGetValue(name, out var sawmill))
                _sawmills.Add(name, sawmill = new Sawmill(this, name));
            return sawmill;
        }
    }

    internal LogLevel ResolveLevel(string channel)
    {
        lock (_config)
        {
            for (var name = channel; ;)
            {
                if (_levels.TryGetValue(name, out var level)) return level;
                int dot = name.LastIndexOf('.');
                if (dot < 0) return DefaultLevel;
                name = name[..dot];
            }
        }
    }

    internal void Write(Sawmill sawmill, LogLevel level, string message, Exception? exception)
    {
        var thread = Thread.CurrentThread;
        var entry = new LogEntry(
            DateTime.UtcNow,
            level,
            sawmill.Name,
            message,
            exception,
            thread.ManagedThreadId,
            thread.Name ?? $"T{thread.ManagedThreadId}"
        );

        if (!_async)
        {
            lock (_syncWrite) Deliver(entry);
            return;
        }

        EnsureWriter().Writer.TryWrite(new Item(entry));
    }

    private Channel<Item> EnsureWriter()
    {
        var queue = _queue;
        if (queue != null) return queue;

        lock (_config)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogManager));
            if (_queue != null) return _queue;

            var created = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });
            _writer = new Thread(() => Drain(created.Reader)) { Name = "Log", IsBackground = true };
            _queue = created;
            _writer.Start();
            return created;
        }
    }

    private void Drain(ChannelReader<Item> reader)
    {
        while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (reader.TryRead(out var item))
            {
                if (item.FlushDone != null)
                {
                    FlushSinks();
                    item.FlushDone.Set();
                }
                else Deliver(item.Entry);
            }
        }
    }

    private void Deliver(in LogEntry entry)
    {
        foreach (var sink in _sinks)
        {
            /* Broken sinks shouldn't take the rest of the ship down. */
            try { sink.Write(entry); }
            catch { }
        }
    }

    private void FlushSinks()
    {
        foreach (var sink in _sinks)
        {
            try { sink.Flush(); }
            catch { }
        }
    }

    public void Flush()
    {
        if (!_async)
        {
            lock (_syncWrite) FlushSinks();
            return;
        }

        var queue = _queue;
        if (queue == null) return;

        using var done = new ManualResetEventSlim();
        if (queue.Writer.TryWrite(new Item(done)))
            done.Wait(TimeSpan.FromSeconds(10));
    }


    /// <summary>
    /// Logs uncaughts as fatal.
    /// </summary>
    public void HookUnhandledExceptions()
    {
        var sawmill = GetSawmill("crash");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            sawmill.Fatal(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()), "Unhandled exception.");
            Flush();
            //throw args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString());
        };
    }

    public void Dispose()
    {
        Thread? writer;
        lock (_config)
        {
            if (_disposed) return;
            _disposed = true;
            writer = _writer;
            _queue?.Writer.TryComplete();
        }

        writer?.Join(TimeSpan.FromSeconds(10));
        FlushSinks();

        foreach (var sink in _sinks)
            if (sink is IDisposable disposable) disposable.Dispose();
    }
}
