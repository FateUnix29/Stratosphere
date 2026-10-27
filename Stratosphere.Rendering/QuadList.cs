namespace Stratosphere.Rendering;

public sealed class QuadList
{
    private struct SortEntry
    {
        public int Depth;
        public float Secondary;
        public int Layer;
        public uint Texture;
        public int Index;
    }

    private DrawQuad[] _quads = new DrawQuad[64];
    private DrawQuad[] _scratch = Array.Empty<DrawQuad>();
    private SortEntry[] _entries = new SortEntry[64];
    private int _count;
    private bool _sorted = true;

    public int Count => _count;

    public ReadOnlySpan<DrawQuad> Quads
    {
        get
        {
            Sort();
            return new ReadOnlySpan<DrawQuad>(_quads, 0, _count);
        }
    }

    public void Add(in DrawQuad quad, int depth = 0, float secondary = 0f, int layer = 0)
    {
        if (_count == _quads.Length)
        {
            Array.Resize(ref _quads, _quads.Length * 2);
            Array.Resize(ref _entries, _entries.Length * 2);
        }

        _quads[_count] = quad;
        _entries[_count] = new SortEntry { Depth = depth, Secondary = secondary, Layer = layer, Texture = quad.Texture.Value, Index = _count };
        _count++;
        _sorted = false;
    }

    public void Clear()
    {
        _count = 0;
        _sorted = true;
    }

    public void Sort()
    {
        if (_sorted) return;

        new Span<SortEntry>(_entries, 0, _count).Sort(static (a, b) =>
        {
            int c = a.Depth.CompareTo(b.Depth);
            if (c != 0) return c;
            c = a.Secondary.CompareTo(b.Secondary);
            if (c != 0) return c;
            c = a.Layer.CompareTo(b.Layer);
            if (c != 0) return c;
            c = a.Texture.CompareTo(b.Texture);
            if (c != 0) return c;
            return a.Index.CompareTo(b.Index);
        });

        if (_scratch.Length < _quads.Length) _scratch = new DrawQuad[_quads.Length];

        for (int i = 0; i < _count; i++)
        {
            _scratch[i] = _quads[_entries[i].Index];
            _entries[i].Index = i;
        }

        (_quads, _scratch) = (_scratch, _quads);
        _sorted = true;
    }
}