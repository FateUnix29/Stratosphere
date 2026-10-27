using System.Runtime.InteropServices;

namespace Stratosphere.Rendering;

public sealed class ViewPacket
{
    public RenderView View;
    public QuadList Quads { get; } = new();
}

/// <summary>
/// Represents everything that'll be drawn in one frame, decided far before any drawing actually begins. Handed to the actual backend later.
/// </summary>
/// <remarks>
/// The fine divider between 'what's visible' vs 'drawing'. The packets might all be different, but the backend will see them all the same.
/// The views are also drawn in the order added.
/// </remarks>
public sealed class RenderFrame
{
    private readonly List<ViewPacket> _packets = new();
    private int _count;

    public ReadOnlySpan<ViewPacket> Views => CollectionsMarshal.AsSpan(_packets)[.._count];

    /// <summary>
    /// Clears, but does keep the memory, so the next frame shouldn't have to ask for much more.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _count; i++) _packets[i].Quads.Clear();
        _count = 0;
    }

    public ViewPacket AddView(in RenderView view)
    {
        if (_count == _packets.Count) _packets.Add(new ViewPacket());

        var packet = _packets[_count++];
        packet.View = view;
        return packet;
    }
}
