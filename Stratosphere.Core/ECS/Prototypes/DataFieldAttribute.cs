namespace Stratosphere.Core.ECS.Prototypes;

/* Still heavily inspired by RobustToolbox. */

/// <summary>
/// An attribute for specifying that something should be part of the YAML.
/// </summary>
public class DataFieldAttribute : Attribute
{
    /* TODO... We should probably implement custom parsers. */

    /// <summary>
    /// The name of the field in the YAML file.
    /// </summary>
    public string Tag { get; internal set; }

    /// <summary>
    /// Whether or not this field is required to be present for the object to deserialize.
    /// Stratosphere will exit if this is true and it is not present.
    /// </summary>
    public readonly bool Required;

    public override string? ToString() => Tag;

    /// <summary>
    /// Marks the datafield as read-only. In other words, to not write it back to YAML.
    /// </summary>
    public readonly bool ReadOnly;

    /// <summary>
    /// Marks the datafield as server-only, and something clients should avoid.
    /// </summary>
    public readonly bool ServerOnly;

    public DataFieldAttribute(
        string tag,
        bool required = false,
        bool readOnly = false,
        bool serverOnly = false
    )
    {
        Tag = tag;
        Required = required;
        ReadOnly = readOnly;
        ServerOnly = serverOnly;
    }
}
