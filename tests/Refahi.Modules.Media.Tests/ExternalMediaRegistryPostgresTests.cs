using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Persistence.Context;
using Refahi.Modules.Media.Infrastructure.Services;
using Source = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaSource;
using Origin = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaOrigin;
using Status = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaStatus;
using Xunit;
namespace Refahi.Modules.Media.Tests;

public sealed class ExternalMediaRegistryPostgresTests
{
    private sealed class DisposablePostgresFact : FactAttribute
    {
        public DisposablePostgresFact()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_MEDIA_TEST_CONNECTION"))) Skip = "Requires a disposable media_cache_test database."; }
    }
    private sealed class Urls : IExternalMediaUrls { public string GetUrl(Guid id) => "https://media.test/media/external/" + id.ToString("N"); }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new DateTimeOffset(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero).AddSeconds(1);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Images : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([255,216,255,1,2,3]) };
            response.Content.Headers.ContentType = new("image/jpeg"); return Task.FromResult(response);
        }
    }
    [DisposablePostgresFact]
    public async Task RegistryOwnsDeduplicationAndLabelsWhileActivityIsBatchedAndPurgePreservesUrls()
    {
        var connection = Environment.GetEnvironmentVariable("REFAHI_MEDIA_TEST_CONNECTION")!;
        Assert.StartsWith("media_cache_test", new NpgsqlConnectionStringBuilder(connection).Database);
        var services = new ServiceCollection();
        services.AddDbContext<MediaDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", "media")));
        services.AddSingleton<IExternalMediaUrls, Urls>(); services.AddScoped<ExternalMediaRegistry>();
        await using var provider = services.BuildServiceProvider();
        using (var migrationScope = provider.CreateScope()) await migrationScope.ServiceProvider.GetRequiredService<MediaDbContext>().Database.MigrateAsync();
        var label = Guid.NewGuid().ToString("N");
        var original = "https://store.snapptrip.com/test/" + Guid.NewGuid().ToString("N") + ".jpeg?signature=AbC%2F123";
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async n =>
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ExternalMediaRegistry>().GetPublicUrlAsync(original, new(n % 2 == 0 ? "Hotels" : "OtherModule", n % 2 == 0 ? "Provider-A-" + label : "Provider-B-" + label));
        }));
        Assert.Single(results.Distinct()); var publicUrl = results[0]!;
        var id = Guid.Parse(publicUrl.Split('/').Last());
        using var scope2 = provider.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();
        var registry = scope2.ServiceProvider.GetRequiredService<ExternalMediaRegistry>();
        var source = await db.Set<Source>().AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(original, source.SourceUrl); Assert.Null(source.ExpiresAt); Assert.Equal(Status.NotCached, source.Status);
        Assert.Equal(2, await db.Set<Origin>().CountAsync(x => x.SourceId == id));
        var path = Path.Combine(Path.GetTempPath(), "media-test-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new ExternalMediaOptions { CachePath = path, MinFreeBytes = 0 });
        using var activity = new ExternalMediaActivity(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<ExternalMediaActivity>.Instance);
        var clock = new Clock(); var handler = new Images();
        try
        {
            using var cache = new ExternalMedia(options, handler, clock, activity: activity);
            await cache.StartAsync(default);
            var resolved = await registry.ResolveAsync(id, default); Assert.NotNull(resolved);
            using (var image = await cache.OpenAsync(id, resolved, default)) { }
            Assert.Equal(source.FileName + ".bin", Path.GetFileName(Assert.Single(Directory.GetFiles(path, "*.bin"))));
            Assert.Null((await db.Set<Source>().AsNoTracking().SingleAsync(x => x.Id == id)).LastAccessedAt);
            await activity.FlushAsync(default);
            var first = await db.Set<Source>().AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(Status.Cached, first.Status); Assert.Equal(clock.Now.UtcDateTime.AddHours(5), first.ExpiresAt);
            for (var n = 0; n < 2; n++) { clock.Now = clock.Now.AddHours(4); using var image = await cache.OpenAsync(id, resolved, default); }
            Assert.Equal(1, handler.Calls);
            await activity.FlushAsync(default);
            var hot = await db.Set<Source>().AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(clock.Now.UtcDateTime.AddHours(5), hot.ExpiresAt); Assert.Equal(first.RefreshAfter, hot.RefreshAfter);
            Assert.Equal(0, await registry.PurgeAsync("NoSuchModule", null, cache, default));
            Assert.Equal(1, await registry.PurgeAsync(null, "Provider-B-" + label, cache, default));
            await activity.FlushAsync(default);
            Assert.Empty(Directory.GetFiles(path, "*.bin"));
            Assert.Equal(Status.NotCached, (await db.Set<Source>().AsNoTracking().SingleAsync(x => x.Id == id)).Status);
            Assert.Equal(publicUrl, await registry.GetPublicUrlAsync(original));
            using (var image = await cache.OpenAsync(id, resolved, default)) { }
            Assert.Equal(2, handler.Calls);
            clock.Now = clock.Now.AddHours(5).AddSeconds(1); Assert.Null(cache.TryOpenCached(id));
            cache.Sweep(); await activity.FlushAsync(default);
            Assert.Empty(Directory.GetFiles(path, "*.bin"));
            Assert.Equal(publicUrl, await registry.GetPublicUrlAsync(original));
            using (var image = await cache.OpenAsync(id, resolved, default)) { }
            Assert.Equal(3, handler.Calls);
            await cache.StopAsync(default);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
