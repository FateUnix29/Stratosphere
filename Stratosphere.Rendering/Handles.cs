namespace Stratosphere.Rendering;

/* Handles, not objects. Almost similar to the Uids of entities. Things that're on the GPU are referred to by the game with a small integer, not a reference.
   Doing this keeps us free of any particular graphics API. It also keeps them cheap to copy into lists, and a stale handle just... Doesn't work, rather than being
   a potential use-after-free. Only the backend will know this number's meaning. */

/// <summary>
/// Texture owned by the rendering backend... The default of 0 is 'no texture'.
/// </summary>
public readonly record struct TextureHandle(uint Value)
{
    public static readonly TextureHandle None = default;
    public bool IsValid => Value != 0;
}

/// <summary>
/// Surface that can be drawn on. Default of 0 is just the screen; A camera that doesn't ask for anything special auto-renders to the window!
/// </summary>
public readonly record struct RenderTargetHandle(uint Value)
{
    public static readonly RenderTargetHandle Screen = default;
    public bool IsScreen => Value == 0;
}
