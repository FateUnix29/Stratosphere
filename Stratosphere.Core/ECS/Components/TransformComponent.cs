using System.Numerics;
using Stratosphere.Core.ECS.Prototypes;
using Stratosphere.Core.Maths;

namespace Stratosphere.Core.ECS.Components;

[RegisterComponent]
public sealed partial class TransformComponent : Component
{
    /// <summary>
    /// Position local to parent's origin, in the parent's orientation.
    /// </summary>
    [DataField("pos")] public Vector3d LocalPosition;

    /// <summary>
    /// Orientation local to parent's orientation. Identity just matches parent's rotation.
    /// </summary>
    [DataField("rot")] public Quaternion LocalRotation = Quaternion.Identity;

    /// <summary>
    /// The 'parent' of this entity. What entity this one is attached to, if any.
    /// No parent means the "parent" is functionally the world the entity is in itself.
    /// </summary>
    public EntityUid Parent { get; internal set; } = EntityUid.INVALID;

    internal List<EntityUid>? _children;

    /// <summary>
    /// Entities directly attached to this one as 'children.' These entities have their Parent field set to this entity.
    /// </summary>
    public IReadOnlyList<EntityUid> Children => _children ?? (IReadOnlyList<EntityUid>)Array.Empty<EntityUid>();

    public override IComponent Clone()
    {
        /* Due to shallowcopy logic, the base implementation would hand the new entity pointers to the parent and the children. To avoid this, we nullify. */
        var copy = (TransformComponent)base.Clone();
        copy.Parent = EntityUid.INVALID;
        copy._children = null;
        return copy;
    }
}
