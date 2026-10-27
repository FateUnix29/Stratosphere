namespace Stratosphere.Core.Logging;

public enum LogLevel : byte
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

public readonly record struct LogEntry(
    DateTime TimeUtc,
    LogLevel Level,
    string Channel,
    string Message,
    Exception? Exception,
    int ThreadId,
    string ThreadName
);

public interface ILogSink
{
    void Write(in LogEntry entry);
    void Flush() { }
}

public static class LogFormat
{
    public static string Format(in LogEntry e)
    {
        var level = e.Level switch
        {
            LogLevel.Debug => "DEBG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR", /* character count mismatch intentional */
            LogLevel.Fatal => "  +FATAL+  ",
            _ => "UNKNOWN LOG LEVEL"
        };

        var line = $"{e.TimeUtc:HH:mm:ss.fff} [{level}] {e.Channel} ({e.ThreadName}) {e.Message}";
        return e.Exception == null ? line : $"{line}\n{e.Exception}";
    }
}
