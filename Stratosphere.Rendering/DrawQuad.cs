using System.Numerics;

namespace Stratosphere.Rendering;

/// <summary>
/// A single flat rectangle to draw. Everything 2D is made of these, as well as sprites placed in 3D.
/// </summary>
/// <remarks>
/// The rectangle is centered upon <see cref="Position"/> , it lies in the XY plane of <see cref="Rotation"/>, it faces the rotated +Z.
/// <see cref="Size"/> is how far it extends along its own X and Y in world units, total. Its top edge is the local +Y.
/// Position is relative to the owning <see cref="RenderView.Origin"/>... See that for why.
///
/// <see cref="Uv"/> is (u0,v0,u1,v1), the corner of the texture shown at the quad's top left and then the one at its bottom right.
/// Because (0,0) is the top left of an image, an ordinary full texture is (0,0,1,1), a mirrored one (1,0,0,1).
/// 
/// If <see cref="Texture"/> isn't valid, the quad is a solid block of <see cref="Color"/>.
/// 
/// The struct here is intentionally kept compact with no references in it on purpose. Making a list of them means making a contiguous block of memory that the CPU can stream through.
/// The whole thing can also be handed to a different thread or copied to a GPU buffer as is.
/// Generally, cache friendliness and ease of memory management.
/// </remarks>
public struct DrawQuad
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector2 Size;
    public Vector4 Uv;
    public Color Color;
    public TextureHandle Texture;
}
