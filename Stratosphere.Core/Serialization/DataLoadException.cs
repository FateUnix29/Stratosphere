using YamlDotNet.RepresentationModel;

namespace Stratosphere.Core.Serialization;

/// <summary>
/// Something is wrong with a data file. The message starts with a location.
/// </summary>
public sealed class DataLoadException : Exception
{
    /// <summary>The file/general source the problem is in.</summary>
    public string SourceName { get; }
    public int Line { get; }

    public DataLoadException(string source, int line, string message) : base($"{source}:{line}: {message}")
    {
        SourceName = source;
        Line = line;
    }
}

/// <summary>
/// One name-value pair from a YAML mapping, with a marker for which file/general source it came from.
/// Inherited prototypes will mix several entries from several files, so each entry should have its own.
/// </summary>
internal readonly record struct YamlField(string Name, YamlNode Key, YamlNode Value, string Source);
