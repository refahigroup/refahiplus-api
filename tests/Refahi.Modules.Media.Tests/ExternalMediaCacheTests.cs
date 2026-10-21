using System.Net;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Services;
using Xunit;
namespace Refahi.Modules.Media.Tests;
public class ExternalMediaCacheTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([255,216,255,1,2,3]) };
            response.Content.Headers.ContentType = new("image/jpeg"); return Task.FromResult(response);
        }
    }
    [Fact] public async Task DownloadsOnceAndReadsCacheAfterRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var handler = new Handler();
        var options = Options.Create(new ExternalMediaOptions { CachePath=dir, MinFreeBytes=0 }); var id=Guid.NewGuid();
        var source = new ExternalMediaSource(new Uri("https://store.snapptrip.com/a.jpeg"));
        try {
            using (var media = new ExternalMedia(options, handler)) {
                await media.StartAsync(default);
                using (var a=await media.OpenAsync(id,source,default)) Assert.Equal("image/jpeg",a.ContentType);
                using (var b=await media.OpenAsync(id,source,default)) Assert.NotNull(b.ETag);
                Assert.Equal(1,handler.Calls); await media.StopAsync(default);
            }
            var next = new Handler(); using var restarted = new ExternalMedia(options,next); await restarted.StartAsync(default);
            using var image = await restarted.OpenAsync(id,source,default); Assert.Equal(0,next.Calls); await restarted.StopAsync(default);
        } finally { Directory.Delete(dir,true); }
    }
    [Fact] public async Task DisabledCacheDoesNotWriteFiles()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var handler=new Handler();
        using var media = new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath=dir, CacheEnabled=false }),handler);
        var source=new ExternalMediaSource(new Uri("https://store.snapptrip.com/a.jpeg"));
        using var image=await media.OpenAsync(Guid.NewGuid(),source,default); using var data=new MemoryStream();
        await image.Stream.CopyToAsync(data); Assert.Equal(6,data.Length); Assert.False(Directory.Exists(dir));
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Fact] public async Task ExpiredContentIsFetchedAgain()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var handler=new Handler(); var clock=new Clock();
        try {
            using var media=new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath=dir, MinFreeBytes=0 }),handler,clock);
            await media.StartAsync(default); var id=Guid.NewGuid(); var source=new ExternalMediaSource(new Uri("https://store.snapptrip.com/a.jpeg"));
            using (var image=await media.OpenAsync(id,source,default)) { }
            clock.Now=clock.Now.AddHours(25);
            using (var image=await media.OpenAsync(id,source,default)) { }
            Assert.Equal(2,handler.Calls); await media.StopAsync(default);
        } finally { Directory.Delete(dir,true); }
    }
    [Fact] public async Task RestartCleansIncompleteFilesAndEvictionPreservesBudget()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var partial=Path.Combine(dir,Guid.NewGuid().ToString("N")+".bin.part"); File.WriteAllText(partial,"partial");
        try {
            using var media=new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath=dir, MinFreeBytes=0,MaxBytes=16384,MaxImageBytes=1024,MaxEntries=1 }),new Handler());
            await media.StartAsync(default); Assert.False(File.Exists(partial));
            var source=new ExternalMediaSource(new Uri("https://store.snapptrip.com/a.jpeg"));
            using (var image=await media.OpenAsync(Guid.NewGuid(),source,default)) { }
            using (var image=await media.OpenAsync(Guid.NewGuid(),new(new Uri("https://store.snapptrip.com/b.jpeg")),default)) { }
            Assert.Single(Directory.GetFiles(dir,"*.bin"));
            Assert.True(Directory.GetFiles(dir).Sum(file=>new FileInfo(file).Length)<=16384);
            await media.StopAsync(default);
        } finally { Directory.Delete(dir,true); }
    }
    [Fact] public async Task HotFilesStillRefreshIndependentlyOfSlidingExpiry()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var handler=new Handler(); var clock=new Clock();
        try
        {
            using var media=new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath=dir, MinFreeBytes=0 }),handler,clock);
            await media.StartAsync(default); var id=Guid.NewGuid(); var source=new ExternalMediaSource(new Uri("https://store.snapptrip.com/hot.jpeg"));
            using (var image=await media.OpenAsync(id,source,default)) { }
            for (var n=0;n<5;n++) { clock.Now=clock.Now.AddHours(4); using var image=await media.OpenAsync(id,source,default); }
            Assert.Equal(1,handler.Calls);
            clock.Now=clock.Now.AddHours(4); using (var image=await media.OpenAsync(id,source,default)) { }
            Assert.Equal(2,handler.Calls); await media.StopAsync(default);
        }
        finally { Directory.Delete(dir,true); }
    }
    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); await Task.Delay(100,ct);
            var response=new HttpResponseMessage(HttpStatusCode.OK) { Content=new ByteArrayContent([255,216,255,1,2,3]) };
            response.Content.Headers.ContentType=new("image/jpeg"); return response;
        }
    }
    [Fact] public async Task ConcurrentRequestsShareOneDownload()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var handler=new DelayedHandler();
        try
        {
            using var media=new ExternalMedia(Options.Create(new ExternalMediaOptions { CachePath=dir, MinFreeBytes=0 }),handler);
            await media.StartAsync(default); var id=Guid.NewGuid(); var source=new ExternalMediaSource(new Uri("https://store.snapptrip.com/concurrent.jpeg"));
            await Task.WhenAll(Enumerable.Range(0,16).Select(async _ => { using var image=await media.OpenAsync(id,source,default); using var output=new MemoryStream(); await image.Stream.CopyToAsync(output); Assert.Equal(6,output.Length); }));
            Assert.Equal(1,handler.Calls); await media.StopAsync(default);
        }
        finally { Directory.Delete(dir,true); }
    }
    [Fact] public async Task UnapprovedHostnameCannotFetch()
    {
        var handler=new Handler(); using var media=new ExternalMedia(Options.Create(new ExternalMediaOptions()),handler);
        await Assert.ThrowsAsync<ExternalMediaException>(()=>media.OpenAsync(Guid.NewGuid(),new(new Uri("https://example.com/a")),default));
        Assert.Equal(0,handler.Calls);
    }
}
