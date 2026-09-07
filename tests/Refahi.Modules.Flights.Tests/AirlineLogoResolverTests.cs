using MediatR;
using Microsoft.Extensions.Configuration;
using Refahi.Modules.Flights.Application.Services.Airlines;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class AirlineLogoResolverTests
{
    [Fact]
    public async Task Resolve_MapsIataAndPrefersFlightAware()
    {
        var sender = new RecordingSender(["IRA"]);
        var resolver = CreateResolver(sender);

        var url = await resolver.ResolveAsync(" IR ", default);
        var presentation = (await resolver.ResolvePresentationsAsync(["IR"], default))["IR"];

        Assert.Equal("https://media.example/airline-logos/flightaware_logos/IRA.png", url);
        Assert.Equal("Iran Air", presentation.Name);
        var candidates = Assert.Single(sender.LastQuery!.CandidateSets);
        Assert.Equal("IRA", candidates.Key);
        Assert.Equal(
            [
                "airline-logos/flightaware_logos/IRA.png",
                "airline-logos/radarbox_logos/IRA.png",
            ],
            candidates.StoragePaths
        );
    }

    [Fact]
    public async Task Resolve_UsesThreeLetterCodeAndFallsBackToRadarbox()
    {
        var sender = new RecordingSender([], ["UTY"]);
        var resolver = CreateResolver(sender);

        var url = await resolver.ResolveAsync(" uty ", default);

        Assert.Equal("https://media.example/airline-logos/radarbox_logos/UTY.png", url);
        Assert.Equal("UTY", Assert.Single(sender.LastQuery!.CandidateSets).Key);
    }

    [Fact]
    public async Task Resolve_BatchDeduplicatesAndReturnsNullForInvalidOrUnknownCodes()
    {
        var sender = new RecordingSender(["IRA"]);
        var resolver = CreateResolver(sender);

        var urls = await resolver.ResolveAsync(["ir", " IR ", "?", "", null, "ZZ"], default);

        Assert.Single(sender.LastQuery!.CandidateSets);
        Assert.Equal("https://media.example/airline-logos/flightaware_logos/IRA.png", urls["IR"]);
        Assert.Null(urls["ZZ"]);
        Assert.Equal(2, urls.Count);
    }

    [Fact]
    public async Task Resolve_ConfigOverrideTakesPrecedenceOverCatalog()
    {
        var sender = new RecordingSender(["UTY"]);
        var configuration = new ConfigurationManager();
        configuration["Flights:AirlineLogos:CodeOverrides:IR"] = "UTY";
        var resolver = new AirlineLogoResolver(sender, configuration);

        await resolver.ResolveAsync("IR", default);

        Assert.Equal("UTY", Assert.Single(sender.LastQuery!.CandidateSets).Key);
    }

    private static AirlineLogoResolver CreateResolver(ISender sender) =>
        new(sender, new ConfigurationManager());

    private sealed class RecordingSender(
        IReadOnlyCollection<string> flightAwareIcaos,
        IReadOnlyCollection<string>? radarboxIcaos = null
    ) : ISender
    {
        public ResolveMediaPublicUrlsQuery? LastQuery { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default
        )
        {
            LastQuery = Assert.IsType<ResolveMediaPublicUrlsQuery>(request);
            var radarbox = radarboxIcaos ?? [];
            var result = LastQuery.CandidateSets.Select(candidate =>
            {
                var url = flightAwareIcaos.Contains(candidate.Key)
                    ? $"https://media.example/airline-logos/flightaware_logos/{candidate.Key}.png"
                    : radarbox.Contains(candidate.Key)
                        ? $"https://media.example/airline-logos/radarbox_logos/{candidate.Key}.png"
                        : null;
                return new ResolvedMediaPublicUrl(candidate.Key, url);
            }).ToList();
            return Task.FromResult((TResponse)(object)result);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
