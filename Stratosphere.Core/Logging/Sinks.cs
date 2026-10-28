namespace Stratosphere.Core.Logging;

public sealed class ConsoleSink : ILogSink
{
    private readonly TextWriter _writer;
    private readonly bool _color;

    public ConsoleSink(bool color = true, TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
        _color = color && writer == null && !Console.IsOutputRedirected;
    }

    public void Write(in LogEntry entry)
    {
        var text = LogFormat.Format(entry);
        if (!_color) { _writer.WriteLine(text); return; }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = entry.Level switch
        {
            LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Info => ConsoleColor.Blue,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.Magenta,
            _ => ConsoleColor.Cyan
        };
        _writer.WriteLine(text); /* TODO: this works for now but maybe only the level tag should be colored. */
        Console.ForegroundColor = previous;
    }

    public void Flush() => _writer.Flush();
}

public sealed class FileSink : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;

    public FileSink(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }

    public void Write(in LogEntry entry) => _writer.WriteLine(LogFormat.Format(entry));
    public void Flush() => _writer.Flush();
    public void Dispose() => _writer.Dispose();
}

public sealed class MemorySink : ILogSink
{
    private readonly List<LogEntry> _entries = new();

    public int Capacity { get; }

    /// <param name="capacity">Maximum capacity. Once hit, old entries are removed for new entries coming in.</param>
    public MemorySink(int capacity = 10_000) { Capacity = capacity; }

    public void Write(in LogEntry entry)
    {
        lock (_entries)
        {
            if (_entries.Count >= Capacity) _entries.RemoveAt(0);
            _entries.Add(entry);
        }
    }

    public LogEntry[] Snapshot() { lock (_entries) return _entries.ToArray(); }
}
