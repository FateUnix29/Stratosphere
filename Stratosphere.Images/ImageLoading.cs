using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Stratosphere.Rendering;

namespace Stratosphere.Images;

public static class ImageLoading
{
    public static TextureInfo Load(this TextureLibrary library, string key, string path, TextureFilter filter = TextureFilter.Nearest)
    {
        var (width, height, rgba) = ReadRgba(path);
        return library.Add(key, width, height, rgba, filter);
    }

    public static (int Width, int Height, byte[] Rgba) ReadRgba(string path)
    {
        try
        {
            using var image = Image.Load<Rgba32>(path);
            return Copy(image);
        }
        catch (ImageFormatException e) { throw new InvalidDataException($"Can't read '{path}': {e.Message}", e); }
    }

    public static (int Width, int Height, byte[] Rgba) ReadRgba(Stream stream, string name = "image")
    {
        try
        {
            using var image = Image.Load<Rgba32>(stream);
            return Copy(image);
        }
        catch (ImageFormatException e) { throw new InvalidDataException($"Can't read '{name}': {e.Message}", e); }
    }

    private static (int, int, byte[]) Copy(Image<Rgba32> image)
    {
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return (image.Width, image.Height, pixels);
    }
}
