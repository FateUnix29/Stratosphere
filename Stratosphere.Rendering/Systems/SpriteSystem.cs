using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Systems;
using Stratosphere.Rendering.Components;

namespace Stratosphere.Rendering.Systems;

[RegisterSystem]
public sealed class SpriteSystem : EntitySystem
{
    private TransformSystem _transforms = null!;
    public override void Initialize() => _transforms = World.GetSystem<TransformSystem>();

    public void Extract(ViewPacket packet, ITextureProvider? textures)
    {
        ref readonly RenderView view = ref packet.View;
        var frustum = Frustum.FromMatrix(view.ViewProjection);

        foreach (var (uid, sprite, _) in World.Query<SpriteComponent, TransformComponent>())
        {
            if (!sprite.Visible || (sprite.Mask & view.Mask) == RenderMask.None) continue;

            var layers = sprite.Layers;
            if (layers.Count == 0) continue;

            var (position, rotation) = _transforms.GetWorldPose(uid);
            Vector3 relative = position.RelativeTo(view.Origin);
            Quaternion frame = sprite.Billboard ? view.Rotation : rotation;

            Vector3 inView = Vector3.Transform(relative, view.ViewMatrix);
            float secondary = view.Sort == SortMode.ViewY ? -inView.Y : inView.Z;

            float pixelsPerUnit = sprite.PixelsPerUnit > 0 ? sprite.PixelsPerUnit : 1f;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.Visible || !Resolve(layer, textures, out var texture)) continue;

                Vector2 size;
                if (layer.Size is { } explicitSize) size = explicitSize;
                else if (texture.IsValid)
                {
                    var pixels = layer.Region is { } region ? new Vector2(region.Width, region.Height) : new Vector2(texture.Width, texture.Height);
                    size = pixels / pixelsPerUnit;
                }
                else size = Vector2.One;

                size *= layer.Scale * sprite.Scale;

                Vector4 uv = UvFor(layer, texture);

                /* if size negative, flip edges */
                if (size.X < 0) { (uv.X, uv.Z) = (uv.Z, uv.X); size.X = -size.X; }
                if (size.Y < 0) { (uv.Y, uv.W) = (uv.W, uv.Y); size.Y = -size.Y; }

                Vector3 offset = Vector3.Transform(new Vector3(layer.Offset * sprite.Scale, 0f), frame);
                Vector3 center = relative + offset;

                if (!frustum.IntersectsSphere(center, 0.5f*size.Length())) continue;

                Quaternion quadRotation = layer.RotationDegrees == 0f
                    ? frame
                    : Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, layer.RotationDegrees * (MathF.PI/180)), frame);

                var quad = new DrawQuad
                {
                    Position = center,
                    Rotation = quadRotation,
                    Size = size,
                    Uv = uv,
                    Color = sprite.Color * layer.Color,
                    Texture = texture.Handle
                };

                packet.Quads.Add(in quad, sprite.DrawDepth, secondary, i);
            }
        }
    }

    private static bool Resolve(SpriteLayer layer, ITextureProvider? textures, out TextureInfo info)
    {
        if (layer._resolved)
        {
            info = layer._info;
            return true;
        }

        if (string.IsNullOrEmpty(layer.Texture))
        {
            info = default; /* solid color for you */
            return true;
        }

        if (textures != null && textures.TryGetTexture(layer.Texture, out info) && info.IsValid)
        {
            layer._info = info;
            layer._resolved = true;
            return true;
        }

        info = default;
        return false;
    }

    private static Vector4 UvFor(SpriteLayer layer, TextureInfo texture)
    {
        if (layer.Region is not { } region || texture.Width <= 0 || texture.Height <= 0)
            return new Vector4(0,0,1,1);

        return new Vector4(
            (float)region.X / texture.Width,
            (float)region.Y / texture.Height,
            (float)(region.X+region.Width) / texture.Width,
            (float)(region.Y+region.Height) / texture.Height
        );
    }
}
