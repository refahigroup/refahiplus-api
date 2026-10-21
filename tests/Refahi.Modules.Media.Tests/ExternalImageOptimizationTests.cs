using System.Net;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace Refahi.Modules.Media.Tests;

public sealed class ExternalImageOptimizationTests
{
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, false));
    }

    private sealed class Supplier(byte[] bytes, bool knownLength = true) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = knownLength ? new ByteArrayContent(bytes) : new UnknownLengthContent(bytes) });
        }
    }

    private static byte[] LargeImage()
    {
        using var bitmap = new SKBitmap(2400, 1200);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var bytes = data.ToArray();
        // Valid JPEG with trailing bytes exercises the real production 5 MiB threshold without a huge test fixture.
        Array.Resize(ref bytes, 6 << 20);
        return bytes;
    }

    private static readonly ExternalMediaSource Source = new(new Uri("https://store.snapptrip.com/large.jpeg"));

    [Fact]
    public async Task ConcurrentHeavyRequestsShareOneDownloadAndOptimizedRepresentation()
    {
        var path = Path.Combine(Path.GetTempPath(), "media-concurrent-opt-" + Guid.NewGuid().ToString("N"));
        var supplier = new Supplier(LargeImage());
        try
        {
            using var media = new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath = path, MinFreeBytes = 0 }), supplier);
            await media.StartAsync(default);
            var id = Guid.NewGuid();
            var etags = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            {
                using var result = await media.OpenAsync(id, Source, default);
                Assert.Equal("image/webp", result.ContentType);
                return result.ETag;
            }));
            Assert.Equal(1, supplier.Calls); Assert.Single(etags.Distinct());
            Assert.Empty(Directory.GetFiles(path, "*.part"));
            await media.StopAsync(default);
        }
        finally { Directory.Delete(path, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HeavyImageIsOptimizedAndCachedWithStableIdAcrossRestart(bool knownLength)
    {
        var directory = Path.Combine(Path.GetTempPath(), "media-opt-" + Guid.NewGuid().ToString("N"));
        var supplier = new Supplier(LargeImage(), knownLength);
        var options = Options.Create(new ExternalMediaOptions { CachePath = directory, MinFreeBytes = 0 });
        var id = Guid.NewGuid(); string? etag;
        try
        {
            using (var media = new ExternalMedia(options, supplier))
            {
                await media.StartAsync(default);
                var url = media.GetUrl(id);
                using (var result = await media.OpenAsync(id, Source, default))
                {
                    Assert.Equal("image/webp", result.ContentType);
                    etag = result.ETag;
                    Assert.NotNull(etag);
                    using var output = new MemoryStream();
                    await result.Stream.CopyToAsync(output);
                    Assert.True(output.Length <= options.Value.MaxImageBytes);
                    using var decoded = SKBitmap.Decode(output.ToArray());
                    Assert.Equal(1600, decoded.Width);
                    Assert.Equal(800, decoded.Height);
                }
                using var cached = await media.OpenAsync(id, Source, default);
                Assert.Equal(etag, cached.ETag);
                Assert.Equal(url, media.GetUrl(id));
                Assert.Equal(1, supplier.Calls);
                Assert.Empty(Directory.GetFiles(directory, "*.part"));
                Assert.Single(Directory.GetFiles(directory, "*.bin"));
                Assert.Equal(Source.FileName, Path.GetFileNameWithoutExtension(Directory.GetFiles(directory, "*.bin").Single()));
                await media.StopAsync(default);
            }
            var nextSupplier = new Supplier([]);
            using var restarted = new ExternalMedia(options, nextSupplier);
            await restarted.StartAsync(default);
            using var restored = await restarted.OpenAsync(id, Source, default);
            Assert.Equal("image/webp", restored.ContentType);
            Assert.Equal(etag, restored.ETag);
            Assert.Equal(0, nextSupplier.Calls);
            await restarted.StopAsync(default);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceByteLimitAppliesWithAndWithoutContentLength(bool knownLength)
    {
        await Rejects(LargeImage(), o => o.MaxSourceBytes = o.MaxImageBytes, "ImageTooLarge", knownLength);
    }

    [Fact]
    public async Task PixelLimitRejectsBeforeDecodeAndLeavesNoPartialFiles()
        => await Rejects(LargeImage(), o => o.MaxSourcePixels = 100, "ImagePixelLimit");

    [Fact]
    public async Task OutputStillHasAHardLimitAfterBoundedQualityAttempts()
        => await Rejects(LargeImage(), o => o.MaxImageBytes = 1, "OptimizedImageTooLarge");

    [Fact]
    public async Task AnimatedHeavyImagesAreRejectedWithoutSilentlyDroppingAnimation()
    {
        // A two-frame GIF with a global palette; padding forces the heavy-image processing path.
        var bytes = Convert.FromHexString("47494638396101000100800000000000FFFFFF21F90400010000002C000000000100010000020244010021F90400010000002C00000000010001000002024C01003B");
        Array.Resize(ref bytes, 6 << 20);
        await Rejects(bytes, _ => { }, "AnimatedImageTooLarge");
    }

    [Fact]
    public async Task CorruptHeavyImageIsRejectedAndNotCached()
    {
        var bytes = new byte[6 << 20]; bytes[0] = 255; bytes[1] = 216; bytes[2] = 255;
        await Rejects(bytes, _ => { }, "InvalidImageContent");
    }

    [Fact]
    public async Task HeavyImageWithoutScratchBudgetUsesFallbackRatherThanUnboundedMemory()
        => await Rejects(LargeImage(), o => o.MaxBytes = 1024, "ImageProcessingStorageUnavailable", status: 503);

    [Fact]
    public async Task DisabledCacheRejectsKnownHeavyImageWithoutCreatingScratchDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var media = new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath = path, CacheEnabled = false }), new Supplier(LargeImage()));
        var error = await Assert.ThrowsAsync<ExternalMediaException>(() => media.OpenAsync(Guid.NewGuid(), Source, default));
        Assert.Equal(503, error.Status);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void OptimizerHonorsCancellationBeforeNativeWork()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ExternalImageOptimizer.Optimize("absent", "absent", new(), canceled.Token));
    }

    [Fact]
    public void OptimizerPreservesTransparencyWithoutUpscaling()
    {
        using var bitmap = new SKBitmap(16, 8);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        CheckPixels(data.ToArray(), decoded =>
        {
            Assert.Equal(16, decoded.Width); Assert.Equal(8, decoded.Height);
            Assert.Equal(0, decoded.GetPixel(0, 0).Alpha);
        });
    }

    [Fact]
    public void OptimizerAppliesExifOrientationBeforeRemovingMetadata()
    {
        var jpeg = LargeImage();
        var orientation = Convert.FromHexString("FFE1002245786966000049492A0008000000010012010300010000000600000000000000");
        var bytes = jpeg[..2].Concat(orientation).Concat(jpeg[2..]).ToArray();
        CheckPixels(bytes, decoded => { Assert.Equal(800, decoded.Width); Assert.Equal(1600, decoded.Height); });
    }

    private static void CheckPixels(byte[] input, Action<SKBitmap> assert)
    {
        var path = Path.Combine(Path.GetTempPath(), "media-pixels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllBytes(Path.Combine(path, "input"), input);
            ExternalImageOptimizer.Optimize(Path.Combine(path, "input"), Path.Combine(path, "output"), new(), default);
            using var decoded = SKBitmap.Decode(Path.Combine(path, "output")); assert(decoded);
        }
        finally { Directory.Delete(path, true); }
    }

    private sealed class CancelDuringDownload(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        private int reads;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (++reads == 2) { cancellation.Cancel(); return ValueTask.FromCanceled<int>(ct); }
            return base.ReadAsync(buffer, ct);
        }
    }

    private sealed class CancelingSupplier(CancellationTokenSource cancellation) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StreamContent(new CancelDuringDownload(LargeImage(), cancellation)) });
    }

    [Fact]
    public async Task CanceledDownloadRemovesItsScratchFilesAndPublishesNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), "media-cancel-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        try
        {
            using var media = new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath = path, MinFreeBytes = 0 }), new CancelingSupplier(cancellation));
            await media.StartAsync(default);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => media.OpenAsync(Guid.NewGuid(), Source, cancellation.Token));
            Assert.Empty(Directory.GetFiles(path, "*.part")); Assert.Empty(Directory.GetFiles(path, "*.bin"));
            await media.StopAsync(default);
        }
        finally { Directory.Delete(path, true); }
    }

    [Fact]
    public async Task MetadataPublishFailureCannotServeAnIncompleteEntryAndCanRecover()
    {
        var path = Path.Combine(Path.GetTempPath(), "media-publish-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var media = new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath = path, MinFreeBytes = 0 }), new Supplier(LargeImage()));
            await media.StartAsync(default);
            var id = Guid.NewGuid();
            var blocker = Path.Combine(path, Source.FileName + ".json"); Directory.CreateDirectory(blocker);
            var error = await Assert.ThrowsAsync<ExternalMediaException>(() => media.OpenAsync(id, Source, default));
            Assert.Equal(502, error.Status); Assert.Null(media.TryOpenCached(id));
            Assert.Empty(Directory.GetFiles(path, "*.part"));
            Directory.Delete(blocker);
            using var recovered = await media.OpenAsync(id, Source, default);
            Assert.Equal("image/webp", recovered.ContentType);
            Assert.NotNull(recovered.ETag);
            await media.StopAsync(default);
        }
        finally { Directory.Delete(path, true); }
    }

    private static async Task Rejects(byte[] bytes, Action<ExternalMediaOptions> configure, string reason, bool knownLength = true, int status = 502)
    {
        var path = Path.Combine(Path.GetTempPath(), "media-reject-" + Guid.NewGuid().ToString("N"));
        var options = new ExternalMediaOptions { CachePath = path, MinFreeBytes = 0 }; configure(options);
        try
        {
            using var media = new ExternalMedia(Options.Create(options), new Supplier(bytes, knownLength));
            await media.StartAsync(default);
            var error = await Assert.ThrowsAsync<ExternalMediaException>(() => media.OpenAsync(Guid.NewGuid(), Source, default));
            Assert.Equal(status, error.Status);
            Assert.Equal(reason, error.Reason);
            Assert.Empty(Directory.GetFiles(path, "*.part"));
            Assert.Empty(Directory.GetFiles(path, "*.bin"));
            await media.StopAsync(default);
        }
        finally { Directory.Delete(path, true); }
    }
}
