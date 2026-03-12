using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace SpoolManager.Infrastructure.Services;

public interface IImageService
{
    byte[] ResizeToThumbnail(byte[] input);
}

public class ImageService : IImageService
{
    private const int MaxDimension = 250;

    public byte[] ResizeToThumbnail(byte[] input)
    {
        using var image = Image.Load(input);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(MaxDimension, MaxDimension),
            Mode = ResizeMode.Max
        }));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 });
        return ms.ToArray();
    }
}
