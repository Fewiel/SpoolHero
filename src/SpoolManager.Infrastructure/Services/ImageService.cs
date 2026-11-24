using SkiaSharp;

namespace SpoolManager.Infrastructure.Services;

public interface IImageService
{
    (byte[] Data, string ContentType) ResizeToThumbnail(byte[] input, string? sourceContentType = null);
    byte[] RenderOgImage(byte[]? logo, byte[]? fallbackIcon);
}

public class ImageService : IImageService
{
    private const int MaxDimension = 250;

    public (byte[] Data, string ContentType) ResizeToThumbnail(byte[] input, string? sourceContentType = null)
    {
        using var source = TryDecode(input)
            ?? throw new InvalidOperationException("Unsupported or invalid image.");

        var scale = Math.Min(1.0, (double)MaxDimension / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = scale < 1.0
            ? source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
            : source.Copy();
        using var image = SKImage.FromBitmap(resized);

        var isPng = sourceContentType?.Contains("png", StringComparison.OrdinalIgnoreCase) == true;
        if (isPng)
        {
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return (png.ToArray(), "image/png");
        }

        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return (jpeg.ToArray(), "image/jpeg");
    }

    public byte[] RenderOgImage(byte[]? logo, byte[]? fallbackIcon)
    {
        const int width = 1200;
        const int height = 630;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(15, 23, 42));

        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        if (logo != null && TryDecode(logo) is { } logoBitmap)
        {
            using (logoBitmap)
            {
                var scale = Math.Min(800f / logoBitmap.Width, 400f / logoBitmap.Height);
                var w = logoBitmap.Width * scale;
                var h = logoBitmap.Height * scale;
                using var img = SKImage.FromBitmap(logoBitmap);
                canvas.DrawImage(img, SKRect.Create((width - w) / 2, (height - h) / 2, w, h), sampling);
            }
        }
        else if (fallbackIcon != null && TryDecode(fallbackIcon) is { } iconBitmap)
        {
            using (iconBitmap)
            {
                using var img = SKImage.FromBitmap(iconBitmap);
                canvas.DrawImage(img, SKRect.Create(450, 165, 300, 300), sampling);
            }
        }

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKBitmap? TryDecode(byte[] bytes)
    {
        try { return SKBitmap.Decode(bytes); }
        catch { return null; }
    }
}
