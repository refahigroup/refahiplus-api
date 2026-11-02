using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Flights.Application.Features.Locations;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Features.Cities.GetCityGeographies;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightGeographyServiceTests
{
    [Fact]
    public async Task Batch_DeduplicatesReferences_AndSkipsInactiveOrMissingWithoutInventingIds()
    {
        var reader = new Reader([new(1,"مرجع","Reference",2,"استان","IR","ایران","Iran",true),
            new(3,"غیرفعال","Inactive",2,"استان","IR","ایران","Iran",false)]);
        var service = Create(new Mappings(new Dictionary<string,int>{{"THR",1},{"IKA",1},{"AAA",3},{"BBB",4}}), reader);
        var actual = await service.ReadAsync(false, ["THR","IKA","AAA","BBB","ZZZ"], default);
        Assert.Equal(2, actual.Count);
        Assert.Equal(actual["THR"], actual["IKA"]);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(new[]{1,3,4}, reader.Ids!.Order().ToArray());
    }

    [Fact]
    public async Task EmptyMappings_DoNotQueryReferences()
    {
        var reader = new Reader([]);
        Assert.Empty(await Create(new Mappings(new Dictionary<string,int>()), reader).ReadAsync(true,["THR"],default));
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task InfrastructureFailure_IsNotConvertedToMissingMapping()
    {
        var reader = new Reader([]) { Failure = new InvalidOperationException("database unavailable") };
        var service = Create(new Mappings(new Dictionary<string,int>{{"THR",1}}),reader);
        Assert.Equal(reader.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(true,["THR"],default)));
    }

    private static FlightGeographyService Create(IFlightGeographyMappingRepository maps, Reader reader) =>
        new(maps, new Mediator(type => type == typeof(IRequestHandler<GetCityGeographiesQuery,IReadOnlyList<CityGeography>>)
            ? new GetCityGeographiesHandler(reader) : Array.Empty<IPipelineBehavior<GetCityGeographiesQuery,IReadOnlyList<CityGeography>>>()),
            NullLogger<FlightGeographyService>.Instance);

    private sealed class Mappings(IReadOnlyDictionary<string,int> rows) : IFlightGeographyMappingRepository
    {
        public Task<IReadOnlyDictionary<string,int>> ReadAsync(bool commercial,IReadOnlyCollection<string>? codes,CancellationToken ct) => Task.FromResult(rows);
    }
    private sealed class Reader(IReadOnlyList<CityGeography> rows) : ICityGeographyReader
    {
        public int Calls; public IReadOnlyCollection<int>? Ids; public Exception? Failure;
        public Task<IReadOnlyList<CityGeography>> ReadAsync(IReadOnlyCollection<int> ids,CancellationToken ct)
        { Calls++; Ids=ids; if(Failure is not null) throw Failure; return Task.FromResult(rows); }
    }
}
