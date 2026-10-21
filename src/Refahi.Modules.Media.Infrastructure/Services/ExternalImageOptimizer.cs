using SkiaSharp;

namespace Refahi.Modules.Media.Infrastructure.Services;

internal static class ExternalImageOptimizer
{
    // Native work is bounded by pixels and serialized by ExternalMedia; cancellation is checked between codec calls.
    internal static void Optimize(string input, string output, ExternalMediaOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var source = new SKFileStream(input);
        using var codec = SKCodec.Create(source);
        if (codec is null) throw new ExternalMediaException(502, "InvalidImageContent");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 16384 || info.Height > 16384
            || (long)info.Width * info.Height > options.MaxSourcePixels)
            throw new ExternalMediaException(502, "ImagePixelLimit");
        if (codec.FrameCount > 1) throw new ExternalMediaException(502, "AnimatedImageTooLarge");

        var scale = Math.Min(1f, (float)options.MaxImageDimension / Math.Max(info.Width, info.Height));
        var decodedSize = codec.GetScaledDimensions(scale);
        using var bitmap = new SKBitmap(new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ExternalMediaException(502, "InvalidImageContent");
        ct.ThrowIfCancellationRequested();

        var swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = Math.Max(1, (int)(info.Width * scale));
        var height = Math.Max(1, (int)(info.Height * scale));
        using var resized = new SKBitmap(swap ? height : width, swap ? width : height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)resized.Width / (swap ? bitmap.Height : bitmap.Width), (float)resized.Height / (swap ? bitmap.Width : bitmap.Height));
            var w = bitmap.Width; var h = bitmap.Height;
            var transform = codec.EncodedOrigin switch
            {
                SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
                SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
                SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
                SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
                SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
                SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
                SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
                _ => SKMatrix.Identity
            };
            canvas.Concat(in transform);
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }

        using var result = SKImage.FromBitmap(resized);
        foreach (var quality in new[] { 80, 65, 50, 35 })
        {
            ct.ThrowIfCancellationRequested();
            using var encoded = result.Encode(SKEncodedImageFormat.Webp, quality);
            if (encoded is null) throw new ExternalMediaException(502, "ImageEncodingFailed");
            ct.ThrowIfCancellationRequested();
            if (encoded.Size > options.MaxImageBytes) continue;
            using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            encoded.SaveTo(destination);
            ct.ThrowIfCancellationRequested();
            return;
        }
        throw new ExternalMediaException(502, "OptimizedImageTooLarge");
    }

    private static SKMatrix Matrix(float a, float b, float c, float d, float e, float f) => new()
        { ScaleX = a, SkewX = b, TransX = c, SkewY = d, ScaleY = e, TransY = f, Persp2 = 1 };
}
