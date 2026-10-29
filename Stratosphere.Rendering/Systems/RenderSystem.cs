using System.Numerics;
using Stratosphere.Core.ECS;
using Stratosphere.Core.ECS.Components;
using Stratosphere.Core.ECS.Systems;
using Stratosphere.Rendering.Components;

namespace Stratosphere.Rendering.Systems;

[RegisterSystem]
public sealed class RenderSystem : EntitySystem
{
    public IRenderDevice? Device { get; set; }
    public ITextureProvider? Textures { get; set; }

    private TransformSystem _transforms = null!;
    private SpriteSystem _sprites = null!;
    private readonly List<(int Order, EntityUid Uid, CameraComponent Camera)> _cameras = new();

    public override void Initialize()
    {
        _transforms = World.GetSystem<TransformSystem>();
        _sprites = World.GetSystem<SpriteSystem>();
    }

    public void BuildFrame(RenderFrame frame)
    {
        var device = Device ?? throw new InvalidOperationException("RenderSystem cannot build a frame without a device.");

        frame.Clear();
        _cameras.Clear();

        foreach (var (uid, camera) in World.Query<CameraComponent>())
            if (camera.Enabled && World.Has<TransformComponent>(uid))
                _cameras.Add((camera.Order, uid, camera));

        _cameras.Sort(static (a,b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Uid.CompareTo(b.Uid));

        foreach (var (_, uid, camera) in _cameras)
        {
            var targetSize = device.GetTargetSize(camera.Target);
            var viewport = PixelViewport(camera.Viewport, targetSize);
            if (viewport.Width <= 0 || viewport.Height <= 0) continue; /* minimized window, probably */

            var view = MakeView(uid, camera, viewport);
            _sprites.Extract(frame.AddView(in view), Textures);
        }
    }

    public void Render()
    {
        var device = Device ?? throw new InvalidOperationException("RenderSystem cannot render without a device.");
        var frame = device.AcquireFrame();

        try { BuildFrame(frame); }
        catch
        {
            /* Can't draw an empty frame, but it has to go back... Draw empty. */
            frame.Clear();
            device.Submit(frame);
            throw;
        }

        device.Submit(frame);
    }

    private RenderView MakeView(EntityUid uid, CameraComponent camera, RectI viewport)
    {
        var (position, rotation) = _transforms.GetWorldPose(uid);
        float aspect = viewport.Width / (float)viewport.Height;

        var projection = camera.Projection == CameraProjection.Orthographic
            ? Matrix4x4.CreateOrthographic(camera.OrthographicSize * aspect, camera.OrthographicSize, camera.Near, camera.Far)
            : Matrix4x4.CreatePerspectiveFieldOfView(camera.FieldOfViewDegrees * (MathF.PI / 180), aspect, camera.Near, camera.Far);

        return new RenderView
        {
            Origin = position,
            Rotation = rotation,

            ViewMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(rotation)),
            ProjectionMatrix = projection,

            Viewport = viewport,
            Target = camera.Target,
            Mask = camera.Mask,
            Sort = camera.Sort,
            Clear = camera.Clear,
            ClearColor = camera.ClearColor
        };
    }

    private static RectI PixelViewport(RectF fraction, SizeI target)
    {
        int x = (int)MathF.Round(fraction.X * target.Width);
        int y = (int)MathF.Round(fraction.Y * target.Height);
        int width = (int)MathF.Round(fraction.Width * target.Width);
        int height = (int)MathF.Round(fraction.Height * target.Height);
        return new RectI(x, y, width, height);
    }
}
