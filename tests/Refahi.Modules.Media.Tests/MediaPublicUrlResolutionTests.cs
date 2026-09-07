using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Modules.Media.Application.Features.ResolveUrls;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Enums;
using Refahi.Modules.Media.Infrastructure.Options;
using Refahi.Modules.Media.Infrastructure.Services;
using Xunit;

namespace Refahi.Modules.Media.Tests;

public sealed class MediaPublicUrlResolutionTests
{
    [Fact]
    public async Task Handler_SelectsFirstExistingCandidate()
    {
        var storage = new RecordingStorage(["logos/radarbox/IRA.png"]);
        var handler = new ResolveMediaPublicUrlsQueryHandler(storage);

        var result = await handler.Handle(
            new ResolveMediaPublicUrlsQuery(
                [
                    new MediaPublicUrlCandidateSet(
                        "IRA",
                        ["logos/flightaware/IRA.png", "logos/radarbox/IRA.png"]
                    ),
                ]
            ),
            default
        );

        Assert.Equal("https://media.example/logos/radarbox/IRA.png", Assert.Single(result).PublicUrl);
        Assert.Equal(
            ["logos/flightaware/IRA.png", "logos/radarbox/IRA.png"],
            storage.CheckedPaths
        );
    }

    [Fact]
    public async Task FileSystemStorage_ChecksExistenceRejectsTraversalAndBuildsCleanUrl()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "refahi-media-test", Guid.NewGuid().ToString("N"));
        var logoPath = Path.Combine(basePath, "airline-logos", "flightaware_logos");
        Directory.CreateDirectory(logoPath);
        await File.WriteAllBytesAsync(Path.Combine(logoPath, "IRA.png"), [1]);

        try
        {
            var storage = new FileSystemMediaStorageService(
                Options.Create(
                    new MediaStorageOptions
                    {
                        BasePath = basePath,
                        LoadBaseUrl = "https://media.example//",
                    }
                ),
                new HttpClient()
            );

            Assert.True(await storage.ExistsAsync("airline-logos/flightaware_logos/IRA.png"));
            Assert.False(await storage.ExistsAsync("airline-logos/flightaware_logos/ZZZ.png"));
            Assert.Equal(
                "https://media.example/airline-logos/flightaware_logos/IRA.png",
                storage.GetPublicUrl("airline-logos/flightaware_logos/IRA.png")
            );
            Assert.Throws<InvalidOperationException>(() => storage.ExistsAsync("../secret.png"));
            Assert.Throws<InvalidOperationException>(() => storage.ExistsAsync("C:/secret.png"));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystemStorage_ResolvesRemoteReadHostWhenFileIsNotLocal()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "refahi-media-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(basePath);
        var httpClient = new HttpClient(new StatusHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/IRA.png", StringComparison.Ordinal)
                ? System.Net.HttpStatusCode.OK
                : System.Net.HttpStatusCode.NotFound
        ));

        try
        {
            var storage = new FileSystemMediaStorageService(
                Options.Create(
                    new MediaStorageOptions
                    {
                        BasePath = basePath,
                        LoadBaseUrl = "https://localhost/media-files",
                        PublicReadBaseUrls = ["https://media.example"],
                    }
                ),
                httpClient
            );

            Assert.Equal(
                "https://media.example/airline-logos/flightaware_logos/IRA.png",
                await storage.ResolvePublicUrlAsync(
                    "airline-logos/flightaware_logos/IRA.png"
                )
            );
            Assert.Null(await storage.ResolvePublicUrlAsync("airline-logos/radarbox_logos/ZZZ.png"));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    private sealed class RecordingStorage(IReadOnlyCollection<string> existingPaths)
        : IMediaStorageService
    {
        public List<string> CheckedPaths { get; } = [];

        public Task<MediaStorageResult> SaveAsync(
            Stream fileStream,
            string fileExtension,
            MediaType mediaType,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task DeleteAsync(string storagePath, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
        {
            CheckedPaths.Add(storagePath);
            return ValueTask.FromResult(existingPaths.Contains(storagePath));
        }

        public Task<string?> ResolvePublicUrlAsync(
            string storagePath,
            CancellationToken ct = default
        )
        {
            CheckedPaths.Add(storagePath);
            return Task.FromResult(
                existingPaths.Contains(storagePath)
                    ? $"https://media.example/{storagePath}"
                    : null
            );
        }

        public string GetPublicUrl(string storagePath) => $"https://media.example/{storagePath}";
    }

    private sealed class StatusHandler(Func<HttpRequestMessage, System.Net.HttpStatusCode> status)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(status(request)));
    }
}
