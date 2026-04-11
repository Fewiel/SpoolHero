using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace SpoolManager.Infrastructure.Services;

public interface IImageService
{
    (byte[] Data, string ContentType) ResizeToThumbnail(byte[] input, string? sourceContentType = null);
}

public class ImageService : IImageService
{
    private const int MaxDimension = 250;

    public (byte[] Data, string ContentType) ResizeToThumbnail(byte[] input, string? sourceContentType = null)
    {
        using var image = Image.Load(input);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(MaxDimension, MaxDimension),
            Mode = ResizeMode.Max
        }));

        using var ms = new MemoryStream();
        var isPng = sourceContentType?.Contains("png", StringComparison.OrdinalIgnoreCase) == true;

        if (isPng)
        {
            image.SaveAsPng(ms, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
            return (ms.ToArray(), "image/png");
        }

        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 85 });
        return (ms.ToArray(), "image/jpeg");
    }
}
