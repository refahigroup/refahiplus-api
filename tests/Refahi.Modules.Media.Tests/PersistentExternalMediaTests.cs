using System.Net;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace Refahi.Modules.Media.Tests;

public sealed class PersistentExternalMediaTests
{
    private sealed class Supplier : HttpMessageHandler
    {
        public int Calls;
        public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Fail) throw new HttpRequestException("Offline");
            using var bitmap = new SKBitmap(100, 50); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 80);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data.ToArray()) });
        }
    }
    [Fact] public async Task PersistentFileSurvivesRestartAndTransientCacheEvictionWithoutNetwork()
    {
        var root = Path.Combine(Path.GetTempPath(), "persistent-media-test-" + Guid.NewGuid().ToString("N"));
        var supplier = new Supplier();
        var external = Options.Create(new ExternalMediaOptions { CacheEnabled = false });
        var policy = Options.Create(new PersistentExternalMediaOptions { Enabled = true, StoragePath = root, MinFreeBytes = 0 });
        try
        {
            Guid id;
            using (var downloader = new ExternalMedia(external, supplier))
            {
                var store = new PersistentExternalMedia(downloader, external, policy);
                var ids = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => store.EnsureAsync("https://store.snapptrip.com/a.png", new("Hotels", "SnappTrip"), default)));
                id = ids[0]; Assert.Single(ids.Distinct()); Assert.Equal(1, supplier.Calls);
            }
            supplier = new Supplier { Fail = true };
            using var restarted = new ExternalMedia(external, supplier);
            var local = new PersistentExternalMedia(restarted, external, policy);
            using var file = local.Open(id); Assert.NotNull(file); Assert.True(file.Length > 0);
            Assert.Equal(id, await local.EnsureAsync("https://store.snapptrip.com/a.png", new("Hotels", "SnappTrip"), default));
            Assert.Equal(0, supplier.Calls); Assert.Contains("/media/persistent/", local.GetUrl(id));
            Assert.Empty(Directory.GetFiles(root, "*.part")); Assert.Empty(Directory.GetFiles(root, "*.input"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task FailedRefreshPreservesReadableLastGoodFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "persistent-media-test-" + Guid.NewGuid().ToString("N"));
        var supplier = new Supplier(); var external = Options.Create(new ExternalMediaOptions { CacheEnabled = false });
        using var downloader = new ExternalMedia(external, supplier);
        var store = new PersistentExternalMedia(downloader, external, Options.Create(new PersistentExternalMediaOptions { Enabled = true, StoragePath = root, MinFreeBytes = 0 }));
        try
        {
            var id = await store.EnsureAsync("https://store.snapptrip.com/a.png", new("Hotels", "SnappTrip"), default);
            File.SetLastWriteTimeUtc(Path.Combine(root, id.ToString("N") + ".webp"), DateTime.UtcNow.AddDays(-2));
            supplier.Fail = true;
            await Assert.ThrowsAnyAsync<Exception>(() => store.EnsureAsync("https://store.snapptrip.com/a.png", new("Hotels", "SnappTrip"), default));
            using var file = store.Open(id); Assert.NotNull(file); Assert.True(file.Length > 0);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task InsufficientStorageCannotReplaceExistingContentOrStartDownload()
    {
        var root = Path.Combine(Path.GetTempPath(), "persistent-media-test-" + Guid.NewGuid().ToString("N"));
        var supplier = new Supplier(); var external = Options.Create(new ExternalMediaOptions { CacheEnabled = false });
        using var downloader = new ExternalMedia(external, supplier);
        var store = new PersistentExternalMedia(downloader, external, Options.Create(new PersistentExternalMediaOptions { Enabled = true, StoragePath = root, MinFreeBytes = long.MaxValue / 2 }));
        try { await Assert.ThrowsAsync<ExternalMediaException>(() => store.EnsureAsync("https://store.snapptrip.com/a.png", new("Hotels", "SnappTrip"), default)); Assert.Equal(0, supplier.Calls); }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task DisabledAndUnapprovedSourcesCannotBeDownloaded()
    {
        using var downloader = new ExternalMedia(Options.Create(new ExternalMediaOptions()), new Supplier());
        var external = Options.Create(new ExternalMediaOptions());
        var disabled = new PersistentExternalMedia(downloader, external, Options.Create(new PersistentExternalMediaOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabled.EnsureAsync("https://store.snapptrip.com/a", new("Hotels"), default));
        var enabled = new PersistentExternalMedia(downloader, external, Options.Create(new PersistentExternalMediaOptions { Enabled = true, StoragePath = Path.GetTempPath() }));
        await Assert.ThrowsAsync<ExternalMediaException>(() => enabled.EnsureAsync("https://unapproved.test/a", new("Hotels"), default));
    }
}
