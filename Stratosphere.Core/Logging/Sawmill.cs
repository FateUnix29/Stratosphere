using System.Runtime.CompilerServices;

namespace Stratosphere.Core.Logging;

/* Robust also calls these sawmills! */

/// <summary>
/// Named channel to log to, such as "render" or "TransformSystem". Get one from the log manager.
/// All systems automatically have one.
/// </summary>
/// <remarks>
/// Prefer the interpolated form, <c>Log.Debug($"Spawned {uid} at {position}")</c>.
/// When Debug is off, the compiler ensures the message is never even built at all, not even the values within, so leaving expensive debug logs in just becomes a single comparison! Magic!
/// </remarks>
public sealed class Sawmill
{
    private readonly LogManager _manager;
    private LogLevel _level;
    private int _levelVersion = -1; /* not a real version so first read does a lookup */

    public string Name { get; }

    internal Sawmill(LogManager manager, string name) { _manager = manager; Name = name; }

    public LogLevel Level
    {
        get
        {
            int version = _manager.LevelVersion;
            if (Volatile.Read(ref _levelVersion) != version)
            {
                _level = _manager.ResolveLevel(Name);
                Volatile.Write(ref _levelVersion, version);
            }
            return _level;
        }
    }

    public bool IsEnabled(LogLevel level) => _manager.HasSinks && level >= Level;

    public void Log(LogLevel level, string message, Exception? exception = null)
    { if (IsEnabled(level)) _manager.Write(this, level, message, exception); }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Fatal(string message) => Log(LogLevel.Fatal, message);

    public void Warning(Exception exception, string message) => Log(LogLevel.Warning, message, exception);
    public void Error(Exception exception, string message) => Log(LogLevel.Error, message, exception);
    public void Fatal(Exception exception, string message) => Log(LogLevel.Fatal, message, exception);

    public void Debug([InterpolatedStringHandlerArgument("")] ref LogHandler<DebugLevel> message) => Finish(ref message);
    public void Info([InterpolatedStringHandlerArgument("")] ref LogHandler<InfoLevel> message) => Finish(ref message);
    public void Warning([InterpolatedStringHandlerArgument("")] ref LogHandler<WarningLevel> message) => Finish(ref message);
    public void Error([InterpolatedStringHandlerArgument("")] ref LogHandler<ErrorLevel> message) => Finish(ref message);
    public void Fatal([InterpolatedStringHandlerArgument("")] ref LogHandler<FatalLevel> message) => Finish(ref message);

    private void Finish<TLevel>(ref LogHandler<TLevel> handler) where TLevel : struct, ILogLevel
    {
        if (handler.Enabled) _manager.Write(this, TLevel.Level, handler.ToStringAndClear(), null);
    }
}

/* Each level, its own tiny type. This is so each interpolated overload above can get its own handler that knows (at compile time) which level it's for. */
public interface ILogLevel { static abstract LogLevel Level { get; } }
public struct DebugLevel : ILogLevel { public static LogLevel Level => LogLevel.Debug; }
public struct InfoLevel : ILogLevel { public static LogLevel Level => LogLevel.Info; }
public struct WarningLevel : ILogLevel { public static LogLevel Level => LogLevel.Warning; }
public struct ErrorLevel : ILogLevel { public static LogLevel Level => LogLevel.Error; }
public struct FatalLevel : ILogLevel { public static LogLevel Level => LogLevel.Fatal; }


/// <summary>
/// The custom handler the compiler uses when dealing with <c>$"..."</c> strings in logging methods. It first asks the channel if anyone is listening.
/// If not, the compiler can skip building the string all together.
/// </summary>
[InterpolatedStringHandler]
public ref struct LogHandler<TLevel> where TLevel : struct, ILogLevel
{
    private DefaultInterpolatedStringHandler _text;
    internal readonly bool Enabled;

    public LogHandler(int literalLength, int formattedCount, Sawmill sawmill, out bool shouldAppend)
    {
        Enabled = shouldAppend = sawmill.IsEnabled(TLevel.Level);
        _text = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string value) => _text.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _text.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _text.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => _text.AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => _text.AppendFormatted(value, alignment, format);

    internal string ToStringAndClear() => _text.ToStringAndClear();
}
