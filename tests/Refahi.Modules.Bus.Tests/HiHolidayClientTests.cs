using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Bus.Application.Contracts;
using Refahi.Modules.Bus.Infrastructure;
using Xunit;

namespace Refahi.Modules.Bus.Tests;

public sealed class HiHolidayClientTests
{
    [LiveHiHolidayFact]
    public async Task RegionsSearchAndDetailUseLiveHiHolidayData()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Environment.GetEnvironmentVariable("BUS_SMOKE_SETTINGS")!)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterBusInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var buses = scope.ServiceProvider.GetRequiredService<IBusProvider>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = stop.Token;

        var regions = await buses.RegionsAsync(ct);
        var origin = FindCity(regions, "BUS_SMOKE_ORIGIN");
        var destination = FindCity(regions, "BUS_SMOKE_DESTINATION");
        Assert.NotEqual(origin.Id, destination.Id);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "Asia/Tehran").DateTime);
        IReadOnlyList<BusProviderService> trips = [];
        for (var day = 1; day <= 3 && trips.Count == 0; day++)
            trips = await buses.SearchAsync(origin.Id, destination.Id, today.AddDays(day), ct);

        Assert.NotEmpty(trips);
        var trip = trips.First();
        var detail = await buses.DetailAsync(trip.Code, ct);
        Assert.Equal(trip.Code, detail.Service.Code);
        Assert.True(detail.Service.PriceMinor > 0);
        Assert.NotEmpty(detail.Seats);
        Assert.All(detail.Seats, seat =>
        {
            Assert.True(seat.Number > 0 && seat.Row > 0 && seat.Column > 0);
            Assert.Equal(seat.Status == "Free", seat.Available);
        });
        Assert.Equal(detail.Seats.Count, detail.Seats.Select(x => (x.Row, x.Column)).Distinct().Count());
    }

    private static BusProviderRegion FindCity(IReadOnlyList<BusProviderRegion> regions, string setting)
    {
        var name = Environment.GetEnvironmentVariable(setting);
        return Assert.Single(regions, x => x.Type == "City" &&
            (x.Name.Equals(name, StringComparison.Ordinal) || x.NameEn.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class LiveHiHolidayFactAttribute : FactAttribute
{
    public LiveHiHolidayFactAttribute()
    {
        if (new[] { "BUS_SMOKE_SETTINGS", "BUS_SMOKE_ORIGIN", "BUS_SMOKE_DESTINATION" }
            .Any(key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))))
            Skip = "Set the local settings path and choose two live HiHoliday cities.";
    }
}
