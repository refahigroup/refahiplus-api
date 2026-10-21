using System.Net;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Services;
using Xunit;

namespace Refahi.Modules.Media.Tests;

public class ExternalMediaSupplierContentTests
{
    private sealed class Supplier(byte[] bytes, string? declaredType, bool failFirst = false) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (failFirst && Calls == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            if (declaredType is not null) response.Content.Headers.ContentType = new(declaredType);
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData(true, "application/octet-stream", "FFD8FFE000104A464946000101", "image/jpeg")]
    [InlineData(false, "application/octet-stream", "FFD8FFE000104A464946000101", "image/jpeg")]
    [InlineData(true, "image/jpeg", "89504E470D0A1A0A0000000D49484452", "image/png")]
    [InlineData(false, "image/jpeg", "89504E470D0A1A0A0000000D49484452", "image/png")]
    [InlineData(true, null, "47494638396101000100", "image/gif")]
    [InlineData(false, null, "524946463132333457454250", "image/webp")]
    public async Task ServesDetectedMimeAndPreservesBytes(bool cached, string? declared, string hex, string expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "media-content-" + Guid.NewGuid().ToString("N"));
        var payload = Convert.FromHexString(hex);
        var supplier = new Supplier(payload, declared);
        try
        {
            using var service = new ExternalMedia(Options.Create(new ExternalMediaOptions
                { CachePath = directory, CacheEnabled = cached, MinFreeBytes = 0 }), supplier);
            await service.StartAsync(default);
            var id = Guid.NewGuid();
            var source = new ExternalMediaSource(new Uri("https://store.snapptrip.com/content.jpeg"));
            using (var image = await service.OpenAsync(id, source, default))
            {
                Assert.Equal(expected, image.ContentType);
                using var output = new MemoryStream();
                await image.Stream.CopyToAsync(output);
                Assert.Equal(payload, output.ToArray());
            }
            if (cached)
            {
                using var image = await service.OpenAsync(id, source, default);
                Assert.Equal(expected, image.ContentType);
                Assert.Equal(1, supplier.Calls);
            }
            await service.StopAsync(default);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task NegativeCacheHitsDoNotExtendRetryDeadline()
    {
        var clock = new Clock();
        var supplier = new Supplier([255,216,255,1,2,3], "application/octet-stream", failFirst: true);
        using var service = new ExternalMedia(Options.Create(new ExternalMediaOptions
            { CacheEnabled = false, NegativeSeconds = 30 }), supplier, clock);
        var id = Guid.NewGuid();
        var source = new ExternalMediaSource(new Uri("https://store.snapptrip.com/retry.jpeg"));
        await Assert.ThrowsAsync<ExternalMediaException>(() => service.OpenAsync(id, source, default));
        clock.Now = clock.Now.AddSeconds(15);
        await Assert.ThrowsAsync<ExternalMediaException>(() => service.OpenAsync(id, source, default));
        Assert.Equal(1, supplier.Calls);
        clock.Now = clock.Now.AddSeconds(16);
        using var image = await service.OpenAsync(id, source, default);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(2, supplier.Calls);
    }
}
