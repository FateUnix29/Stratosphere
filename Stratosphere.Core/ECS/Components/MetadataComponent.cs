using Stratosphere.Core.ECS.Prototypes;

namespace Stratosphere.Core.ECS.Components;

[RegisterComponent]
public sealed partial class MetadataComponent : Component
{
    /* This RobustToolbox inspired stuff looks weird at first, but it's pretty clever.
       What we're doing here is allowing for individual entities to have different names, instead of being locked to the name the prototype provides. */
    [DataField("name")] public string? _entityName;
    [DataField("desc")] public string? _entityDesc;
    internal EntityPrototype? _entityPrototype;

    public string EntityName
    {
        get
        {
            if (_entityName == null)
            {
                if (_entityPrototype != null && _entityPrototype.DisplayName != null) return _entityPrototype.DisplayName;
                return string.Empty;
            }
            return _entityName;
        }
    }

    public string EntityDescription
    {
        get
        {
            if (_entityDesc == null)
            {
                if (_entityPrototype != null && _entityPrototype.Description != null) return _entityPrototype.Description;
                return string.Empty;
            }
            return _entityDesc;
        }
    }

    public string EntityPrototype
    {
        get
        {
            if (_entityPrototype != null) return _entityPrototype.ID;
            return string.Empty;
        }
    }

    /* Currently, this component is only used to store per-entity name and description information. */
}
