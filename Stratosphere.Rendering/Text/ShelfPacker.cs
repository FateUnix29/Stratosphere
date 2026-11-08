namespace Stratosphere.Rendering.Text;

public static class ShelfPacker
{
    public static bool TryPack(SizeI[] sizes, int width, int padding, int maxHeight, Span<(int X, int Y)> positions, out int height)
    {
        height = 0;
        if (positions.Length < sizes.Length) throw new ArgumentException("positions needs a slot for every size.", nameof(positions));

        var order = new int[sizes.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int c = sizes[b].Height.CompareTo(sizes[a].Height);
            return c != 0 ? c : a.CompareTo(b);
        });

        int x = padding;
        int y = padding;
        int rowHeight = 0;

        foreach (int i in order)
        {
            var (w, h) = (sizes[i].Width, sizes[i].Height);
            if (w+2 * padding > width) return false;
            if (x+w + padding > width)
            {
                x = padding;
                y += rowHeight+padding;
                rowHeight =0;
            }

            if (y+h + padding > maxHeight) return false;

            positions[i] = (x,y);
            x += w + padding;
            rowHeight = Math.Max(rowHeight,h);
        }

        height = sizes.Length == 0 ? 0 : y + rowHeight + padding;
        return true;
    }
}
