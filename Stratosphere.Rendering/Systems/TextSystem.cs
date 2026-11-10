using System.Numerics;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Systems;
using Stratosphere.Rendering.Components;
using Stratosphere.Rendering.Text;

namespace Stratosphere.Rendering.Systems;

[RegisterSystem]
public sealed class TextSystem : EntitySystem
{
    private const int TextLayer = 1 << 20;
    private readonly TextLayouter _layouter = new();
    private TransformSystem _transforms = null!;

    public override void Initialize() => _transforms = World.GetSystem<TransformSystem>();

    public Vector2 Measure(TextComponent text, IFontProvider? fonts)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryLayout(text, fonts, out _) ? text.LaidOutSize : Vector2.Zero;
    }

    public void Extract(ViewPacket packet, IFontProvider? fonts)
    {
        ref readonly RenderView view = ref packet.View;
        var frustum = Frustum.FromMatrix(view.ViewProjection);

        foreach (var (uid, text, _) in World.Query<TextComponent, TransformComponent>())
        {
            if (!text.Visible || (text.Mask & view.Mask) == RenderMask.None) continue;
            if (!TryLayout(text, fonts, out var font)) continue;

            var glyphs = text.Glyphs;
            var atlas = font.Atlas;
            if (glyphs.Count == 0 || !atlas.IsValid || atlas.Width <= 0 || atlas.Height <= 0) continue;

            var (position, rotation) = _transforms.GetWorldPose(uid);
            Vector3 relative = position.RelativeTo(view.Origin);
            Quaternion frame = text.Billboard ? view.Rotation : rotation;

            Vector3 inView = Vector3.Transform(relative, view.ViewMatrix);
            float secondary = view.Sort == SortMode.ViewY ? -inView.Y : inView.Z;

            float pixelsPerUnit = text.PixelsPerUnit > 0 ? text.PixelsPerUnit : 1f;
            Vector2 unit = text.Scale / pixelsPerUnit;

            Vector2 block = text.LaidOutSize;
            Vector2 pivot = new(MathF.Floor(text.Pivot.X * block.X + 0.5f), MathF.Floor(text.Pivot.Y * block.Y + 0.5f));

            var blockMiddle = new Vector3((block.X * 0.5f - pivot.X) * unit.X, -(block.Y * 0.5f - pivot.Y) * unit.Y, 0f);
            Vector3 blockCenter = relative + Vector3.Transform(blockMiddle, frame);
            float radius = 0.5f * new Vector2(block.X * MathF.Abs(unit.X), block.Y * MathF.Abs(unit.Y)).Length();
            if (!frustum.IntersectsSphere(blockCenter, radius)) continue;

            Color color = text.Color;
            float atlasW = atlas.Width, atlasH = atlas.Height;

            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                var rect = glyph.Atlas;

                var middle = new Vector3(
                    (glyph.Position.X + rect.Width * 0.5f - pivot.X) * unit.X,
                    -(glyph.Position.Y + rect.Height * 0.5f - pivot.Y) * unit.Y,
                    0f
                );

                var uv = new Vector4(rect.X / atlasW, rect.Y / atlasH, (rect.X + rect.Width) / atlasW, (rect.Y + rect.Height) / atlasH);

                if (unit.X < 0) (uv.X, uv.Z) = (uv.Z, uv.X);
                if (unit.Y < 0) (uv.Y, uv.W) = (uv.W, uv.Y);

                /* at last. */
                var quad = new DrawQuad
                {
                    Position = relative + Vector3.Transform(middle, frame),
                    Rotation = frame,
                    Size = new Vector2(rect.Width * MathF.Abs(unit.X), rect.Height * MathF.Abs(unit.Y)),
                    Uv = uv,
                    Color = color,
                    Texture = atlas.Handle
                };

                packet.Quads.Add(in quad, text.DrawDepth, secondary, TextLayer);
            }
        }
    }

    private bool TryLayout(TextComponent text, IFontProvider? fonts, out IFontFace font)
    {
        font = null!;
        if (fonts == null || text.Font == null || !fonts.TryGetFont(text.Font, out font!)) return false;
        if (!text.Dirty && ReferenceEquals(text.LaidOutWith, font)) return true;

        text.Glyphs.Clear();
        var options = new TextLayoutOptions { MaxWidth = text.MaxWidth, Align = text.Align, LineGap = text.LineGap };
        text.LaidOutSize = _layouter.Layout(font, text.Text, in options, text.Glyphs);
        text.LaidOutWith = font;
        text.Dirty = false;
        return true;
    }
}
