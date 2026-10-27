using System.Numerics;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Events;
using Stratosphere.Core.Maths;

namespace Stratosphere.Core.ECS.Systems;

[RegisterSystem]
public sealed class TransformSystem : EntitySystem
{
    private const int MaxDepth = 4096; /* anything that got this fucking deep is a loop */
    private const int WarningDepth = 1024; /* warning log because this is concerning and potentially bad for performance */

    public override void Initialize()
        => SubscribeLocalEvent<TransformComponent, ComponentShutdown>(OnShutdown);

    private void OnShutdown(EntityUid uid, TransformComponent transform, ref ComponentShutdown args)
    {
        if (transform._children is { Count: > 0 })
            foreach (var child in transform._children.ToArray())
                World.Destroy(child); /* ruthlessly murder children */

        if (transform.Parent.IsValid() && World.TryGet<TransformComponent>(transform.Parent, out var parent))
            parent._children?.Remove(uid);

        transform.Parent = EntityUid.INVALID;
    }

    public (Vector3d Position, Quaternion Rotation) GetWorldPose(EntityUid entity)
    {
        var transform = World.Get<TransformComponent>(entity);
        var position = transform.LocalPosition;
        var rotation = transform.LocalRotation;

        var parent = transform.Parent;
        /* resolve and flatten all of the positions, slowly. */
        for (int depth = 0; parent.IsValid(); depth++)
        {
            if (depth > MaxDepth) throw new InvalidOperationException($"Transform hierarchy above {entity} is cyclic or absurdly deep.");
            if (depth > WarningDepth) { } /* TODO logging is required */
            var up = World.Get<TransformComponent>(parent); /* because what do you mean you dont have a transform component? */

            /* to move from a child space to a parent space, first turn the way the parent is turned, then shift by where it is. */
            position = up.LocalPosition + Vector3d.Rotate(up.LocalRotation, position);

            /* then, we concatenate in an a-then-b order, which is good, because the parent's rotation must come first. */
            rotation = Quaternion.Concatenate(rotation, up.LocalRotation);
            parent = up.Parent;
        }

        return (position, rotation);
    }

    public Vector3d GetWorldPosition(EntityUid entity) => GetWorldPose(entity).Position;
    public Quaternion GetWorldRotation(EntityUid entity) => GetWorldPose(entity).Rotation;

    public void SetWorldPose(EntityUid entity, Vector3d position, Quaternion rotation)
    {
        var transform = World.Get<TransformComponent>(entity);
        (transform.LocalPosition, transform.LocalRotation) = WorldToLocal(transform.Parent, position, rotation);
    }

    public void SetWorldPosition(EntityUid entity, Vector3d position) => SetWorldPose(entity, position, GetWorldRotation(entity));

    public void SetParent(EntityUid child, EntityUid newParent, bool keepWorldPose = true)
    {
        var transform = World.Get<TransformComponent>(child);
        if (transform.Parent == newParent) return;

        if (newParent.IsValid())
        {
            if (!World.TryGet<TransformComponent>(newParent, out _)) throw new InvalidOperationException($"Can't parent {child} to {newParent} because where the fuck is the parent's transform.");
            if (newParent == child || IsDescendantOf(newParent, child)) throw new InvalidOperationException($"Can't parent {child} to itself (or an descendant of itself).");
        }

        var (worldPosition, worldRotation) = GetWorldPose(child);

        if (transform.Parent.IsValid() && World.TryGet<TransformComponent>(transform.Parent, out var oldParent))
            oldParent._children?.Remove(child);

        transform.Parent = newParent;

        if (newParent.IsValid())
        {
            var newParentTransform = World.Get<TransformComponent>(newParent);
            (newParentTransform._children ??= new()).Add(child);
        }

        if (keepWorldPose)
            (transform.LocalPosition, transform.LocalRotation) = WorldToLocal(newParent, worldPosition, worldRotation);
    }

    public void Detach(EntityUid entity, bool keepWorldPose = true) => SetParent(entity, EntityUid.INVALID, keepWorldPose);

    public bool IsDescendantOf(EntityUid entity, EntityUid ancestor)
    {
        if (!World.TryGet<TransformComponent>(entity, out var transform)) return false; /* ???? */
        var parent = transform.Parent;

        for (int depth = 0; parent.IsValid() && depth <= MaxDepth; depth++)
        {
            if (parent == ancestor) return true;
            if (!World.TryGet<TransformComponent>(parent, out var up)) return false;
            parent = up.Parent;
        }

        return false;
    }

    private (Vector3d Position, Quaternion Rotation) WorldToLocal(EntityUid parent, Vector3d worldPosition, Quaternion worldRotation)
    {
        if (!parent.IsValid()) return (worldPosition, worldRotation);

        var (parentPosition, parentRotation) = GetWorldPose(parent);
        var inverse = Quaternion.Conjugate(parentRotation);

        var localPosition = Vector3d.Rotate(inverse, worldPosition - parentPosition);
        var localRotation = Quaternion.Concatenate(worldRotation, inverse);

        return (localPosition, localRotation);
    }
}
