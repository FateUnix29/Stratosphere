using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Stratosphere.Rendering.OpenGL;

public sealed class GLBackend : IRenderBackend
{
    private const int FloatsPerVertex = 9; /* position is 3... + uv is 2... + color is 4. 3+2+4=5+4=9. */
    private const int VerticesPerQuad = 6;

    private readonly NativeWindow _window;
    private readonly Dictionary<uint, int> _textures = new();
    private readonly Dictionary<uint, int> _targets = new();
    private readonly Dictionary<uint, SizeI> _targetSizes = new();

    private readonly int _program, _vao, _vbo, _whiteTexture;
    private readonly int _viewProjectionLocation, _flipYLocation;
    private readonly float[] _matrix = new float[16];

    private float[] _vertices = new float[FloatsPerVertex * VerticesPerQuad * 256];
    private readonly List<Batch> _batches = new();

    private volatile bool _closeRequested;
    private SizeI _screenSize;
    private bool _disposed;

    private readonly record struct Batch(int Texture, int FirstVertex, int VertexCount);

    public GLBackend(GLBackendSettings? settings = null)
    {
        settings ??= new GLBackendSettings();

        /* OpenTK prefers that GLFW be used from the first thread, but GLFW is actually just fine with everything being done on one thread, not necessarily the first.
           MacOS is, as usual, an apple product, so it decides to fuck off unless we give it the real first thread.
           That's okay. I hate Mac anyway. */

        GLFWProvider.CheckForMainThread = false;

        _window = new NativeWindow(new NativeWindowSettings
        {
            Title = settings.Title,
            ClientSize = new OpenTK.Mathematics.Vector2i(settings.Width, settings.Height),
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3,3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            Vsync = settings.VSync ? VSyncMode.On : VSyncMode.Off,
            AutoLoadBindings = false /* done below instead where it can't silently not */
        });

        GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        _window.Closing += _ => _closeRequested = true;

        UpdateScreenSize();

        _program = BuildProgram();
        _viewProjectionLocation = GL.GetUniformLocation(_program, "uViewProjection");
        _flipYLocation = GL.GetUniformLocation(_program, "uFlipY");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        int stride = FloatsPerVertex * sizeof(float);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3*sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 5*sizeof(float));

        _whiteTexture = UploadTexture(1, 1, new byte[] { 255, 255, 255, 255 }, TextureFilter.Nearest);
        ThrowOnGLError("setting up");
    }

    public bool CloseRequested => _closeRequested;

    public SizeI ScreenSize => _screenSize;

    public void Pump()
    {
        NativeWindow.ProcessWindowEvents(false);
        UpdateScreenSize();
    }

    public void UpdateScreenSize()
    {
        var size = _window.FramebufferSize;
        _screenSize = new SizeI(size.X, size.Y);
    }

    public void CreateTexture(TextureHandle handle, in TextureDescription description, ReadOnlySpan<byte> rgba8)
    {
        if (description.Width <= 0 || description.Height <= 0 || rgba8.Length != description.Width * description.Height * 4)
            throw new ArgumentException($"Texture {handle.Value} is {description.Width}*{description.Height}, which needs {description.Width*description.Height*4} bytes of pixels, got {rgba8.Length}");

        _textures[handle.Value] = UploadTexture(description.Width, description.Height, rgba8, description.Filter);
        ThrowOnGLError($"uploading texture {handle.Value}");
    }

    public void DestroyTexture(TextureHandle handle) { if (_textures.Remove(handle.Value, out var texture)) GL.DeleteTexture(texture); }

    public void CreateRenderTarget(RenderTargetHandle handle, TextureHandle color, in RenderTargetDescription description)
    {
        int texture = UploadTexture(description.Width, description.Height, ReadOnlySpan<byte>.Empty, description.Filer);
        int framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            GL.DeleteFramebuffer(framebuffer);
            GL.DeleteTexture(texture);
            throw new InvalidOperationException($"OpenGL failed to make a {description.Width}*{description.Height} render target ({status}).");
        }

        _textures[color.Value] = texture;
        _targets[handle.Value] = framebuffer;
        _targetSizes[handle.Value] = new SizeI(description.Width, description.Height);
        ThrowOnGLError($"creating render target {handle.Value}");
    }

    public void DestroyRenderTarget(RenderTargetHandle handle)
    {
        if (_targets.Remove(handle.Value, out var framebuffer)) GL.DeleteFramebuffer(framebuffer);
        _targetSizes.Remove(handle.Value);
    }

    public void Render(RenderFrame frame)
    {
        NativeWindow.ProcessWindowEvents(false);
        UpdateScreenSize();

        if (_screenSize.Width <= 0 || _screenSize.Height <= 0) return;

        GL.UseProgram(_program);
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.ScissorTest);

        foreach (var packet in frame.Views)
            DrawView(packet);

        GL.Disable(EnableCap.ScissorTest);
        _window.Context.SwapBuffers();
        ThrowOnGLError("rendering frame");
    }

    private void DrawView(ViewPacket packet)
    {
        ref readonly var view = ref packet.View;

        SizeI size;
        bool toScreen = view.Target.IsScreen;
        if (toScreen)
        {
            size = _screenSize;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        else
        {
            if (!_targets.TryGetValue(view.Target.Value, out var framebuffer)) return;
            size = _targetSizes[view.Target.Value];
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        }

        var r = view.Viewport;
        int glY = toScreen ? size.Height - r.Y - r.Height : r.Y;
        GL.Viewport(r.X, glY, r.Width, r.Height);
        GL.Scissor(r.X, glY, r.Width, r.Height);

        if (view.Clear)
        {
            GL.ClearColor(view.ClearColor.R, view.ClearColor.G, view.ClearColor.B, view.ClearColor.A);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }

        BuildBatches(packet.Quads.Quads);
        if (_batches.Count == 0) return;

        var m = view.ViewProjection;
        /* Systems.Numerics uses row-vector (v*M), stored by rows... OpenGL reads those 16 floats as columns, which comes out to it written as M*v. */
        _matrix[0] = m.M11; _matrix[1] = m.M12; _matrix[2] = m.M13; _matrix[3] = m.M14;
        _matrix[4] = m.M21; _matrix[5] = m.M22; _matrix[6] = m.M23; _matrix[7] = m.M24;
        _matrix[8] = m.M31; _matrix[9] = m.M32; _matrix[10] = m.M33; _matrix[11] = m.M34;
        _matrix[12] = m.M41; _matrix[13] = m.M42; _matrix[14] = m.M43; _matrix[15] = m.M44;
        GL.UniformMatrix4(_viewProjectionLocation, 1, false, _matrix);
        GL.Uniform1(_flipYLocation, toScreen ? 1f : -1f);

        int totalVertices = _batches[^1].FirstVertex + _batches[^1].VertexCount;
        GL.BufferData(BufferTarget.ArrayBuffer, totalVertices*FloatsPerVertex*sizeof(float), _vertices, BufferUsageHint.StreamDraw);

        foreach (var batch in _batches)
        {
            GL.BindTexture(TextureTarget.Texture2D, batch.Texture);
            GL.DrawArrays(PrimitiveType.Triangles, batch.FirstVertex, batch.VertexCount);
        }
    }

    private void BuildBatches(ReadOnlySpan<DrawQuad> quads)
    {
        _batches.Clear();

        int needed = quads.Length * VerticesPerQuad* FloatsPerVertex;
        if (_vertices.Length < needed) _vertices = new float[Math.Max(needed, _vertices.Length*2)];

        int vertex = 0;
        int currentTexture = -1;
        int batchStart = 0;

        foreach (ref readonly var quad in quads)
        {
            int texture;
            if (!quad.Texture.IsValid) texture = _whiteTexture;
            else if (!_textures.TryGetValue(quad.Texture.Value, out texture)) continue; /* texture vanished, draw nothing instead of likely garbage */

            if (texture != currentTexture)
            {
                if (currentTexture != -1) _batches.Add(new Batch(currentTexture, batchStart, vertex - batchStart));
                currentTexture = texture;
                batchStart = vertex;
            }

            WriteQuad(in quad, vertex*FloatsPerVertex);
            vertex += VerticesPerQuad;
        }

        if (currentTexture != -1) _batches.Add(new Batch(currentTexture, batchStart, vertex - batchStart));
    }

    private void WriteQuad(in DrawQuad quad, int at)
    {
        float hw = quad.Size.X * 0.5f, hh = quad.Size.Y * 0.5f;

        /* Conversion between the quad's corners in its own space and into the view's space. uvs' y goes down, top corners have smaller v. */
        var topLeft = quad.Position + Vector3.Transform(new Vector3(-hw, hh, 0), quad.Rotation);
        var topRight = quad.Position + Vector3.Transform(new Vector3(hw, hh, 0), quad.Rotation);
        var bottomLeft = quad.Position + Vector3.Transform(new Vector3(-hw, -hh, 0), quad.Rotation);
        var bottomRight = quad.Position + Vector3.Transform(new Vector3(hw, -hh, 0), quad.Rotation);

        float u0 = quad.Uv.X, v0 = quad.Uv.Y, u1 = quad.Uv.Z, v1 = quad.Uv.W;
        var c = quad.Color;

        Vertex(ref at, topLeft, u0, v0, c);
        Vertex(ref at, bottomLeft, u0, v1, c);
        Vertex(ref at, bottomRight, u1, v1, c);
        Vertex(ref at, topLeft, u0, v0, c);
        Vertex(ref at, bottomRight, u1, v1, c);
        Vertex(ref at, topRight, u1, v0, c);
    }

    private void Vertex(ref int at, Vector3 p, float u, float v, Color c)
    {
        var d = _vertices;
        d[at++] = p.X; d[at++] = p.Y; d[at++] = p.Z;
        d[at++] = u; d[at++] = v;
        d[at++] = c.R; d[at++] = c.G; d[at++] = c.B; d[at++] = c.A;
    }


    private static unsafe int UploadTexture(int width, int height, ReadOnlySpan<byte> rgba8, TextureFilter filter)
    {
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);

        int filterMode = filter == TextureFilter.Nearest ? (int)TextureMinFilter.Nearest : (int)TextureMinFilter.Linear;
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filterMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filterMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        fixed (byte* pixels = rgba8)
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba8.IsEmpty ? IntPtr.Zero : (IntPtr)pixels);

        return texture;
    }

    private static int BuildProgram()
    {
        int vertex = Compile(ShaderType.VertexShader, VertexSource);
        int fragment = Compile(ShaderType.FragmentShader, FragmentSource);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertex);
        GL.AttachShader(program, fragment);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException($"OpenGL couldn't link sprite shader: {GL.GetProgramInfoLog(program)}");

        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);
        return program;
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException($"OpenGL couldn't compile {type}: {GL.GetShaderInfoLog(shader)}");
        return shader;
    }

    private static void ThrowOnGLError(string doing)
    {
        var error = GL.GetError();
        if (error != ErrorCode.NoError) throw new InvalidOperationException($"OpenGL reported {error} while {doing}.");
    }


    private const string VertexSource = @"#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aUv;
layout(location = 3) in vec4 aColor;

uniform mat4 uViewProjection;
uniform float uFlipY; /* -1 when drawing into render target, so that way the texture isn't upside down. */

out vec2 vUv;
out vec4 vColor;

void main()
{
    vec4 clip = uViewProjection * vec4(aPosition, 1.0);
    clip.z = clip.z * 2.0 - clip.w; /* engine depth 0..1 -> openGL -1..1 */
    clip.y *= uFlipY;
    gl_Position = clip;
    vUv = aUv;
    vColor = aColor;
}
";
    private const string FragmentSource = @"#version 330 core
in vec2 vUv;
in vec4 vColor;

uniform sampler2D uTexture;

out vec4 outColor;

void main()
{
    outColor = texture(uTexture, vUv) * vColor;
}
";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var texture in _textures.Values) GL.DeleteTexture(texture);
        foreach (var framebuffer in _targets.Values) GL.DeleteFramebuffer(framebuffer);

        GL.DeleteTexture(_whiteTexture);
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_program);

        _window.Dispose();
    }
}
