namespace Stratosphere.Rendering.OpenGL;

public sealed record GLBackendSettings
{
    public string Title { get; init; } = "Stratosphere Window";

    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;

    public bool VSync { get; init; } = true;
}
