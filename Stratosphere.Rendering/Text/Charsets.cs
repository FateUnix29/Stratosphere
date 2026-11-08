namespace Stratosphere.Rendering.Text;

public static class Charsets
{
    public static IEnumerable<int> PrintableAscii() => Range(0x20, 0x7E);
    public static IEnumerable<int> Latin1() => Range(0x20, 0x7E).Concat(Range(0xA0, 0xFF));

    public static IEnumerable<int> Range(int first, int lastInclusive)
    { for (int cpt = first; cpt <= lastInclusive; cpt++) yield return cpt; }
}
