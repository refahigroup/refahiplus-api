using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Bus.Application.Contracts;

namespace Refahi.Modules.Bus.Infrastructure.Providers.HiHoliday;

internal sealed class HiHolidayProvider(HiHolidayClient client, IMemoryCache cache, ILogger<HiHolidayProvider> logger) : IBusProvider
{
    public async Task<IReadOnlyList<BusProviderRegion>> RegionsAsync(CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync(typeof(BusProviderRegion), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return Parse(await client.RegionsAsync(ct), root => root.GetProperty("Regions").EnumerateArray()
                .Select(ReadRegion).ToArray());
        }))!;
    }

    public async Task<IReadOnlyList<BusProviderService>> SearchAsync(int originRegionId, int destinationRegionId,
        DateOnly date, CancellationToken ct) =>
        Parse(await client.SearchAsync(originRegionId, destinationRegionId, date, ct), root => root.GetProperty("Buses")
            .EnumerateArray().Where(bus => !bus.GetProperty("IsForeign").GetBoolean()).Select(ReadService).ToArray());

    public async Task<BusProviderDetail> DetailAsync(string code, CancellationToken ct)
    {
        if (!long.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new BusRequestException("شناسهٔ سرویس اتوبوس معتبر نیست.", 502);

        return Parse(await client.DetailAsync(id, null, ct), root =>
        {
            var bus = root.GetProperty("Bus");
            var service = ReadService(bus);
            if (service.Code != code) throw new FormatException();
            var layout = bus.GetProperty("BusDetail");
            var columns = layout.GetProperty("Col").GetInt32();
            var rows = layout.GetProperty("Row").GetInt32();
            var aisleAfter = layout.GetProperty("Space").GetInt32();
            if (rows is < 1 or > 100 || columns is < 1 or > 8 || aisleAfter < 0 || aisleAfter > columns)
                throw new FormatException();
            var seats = layout.GetProperty("Seats").EnumerateArray()
                .Where(seat => seat.GetProperty("SeatNumber").GetInt32() > 0)
                .Select(seat =>
                {
                    var cell = seat.GetProperty("CellNumber").GetInt32();
                    if (cell < 1 || cell > rows * columns) throw new FormatException();
                    var column = (cell - 1) % columns + 1;
                    var status = seat.GetProperty("Status").GetString();
                    return new BusSeatDto(seat.GetProperty("SeatNumber").GetInt32(), (cell - 1) / columns + 1,
                        column + (aisleAfter > 0 && column > aisleAfter ? 1 : 0), status == "Free", status);
                }).ToArray();
            if (seats.Select(s => s.Number).Distinct().Count() != seats.Length ||
                seats.Select(s => (s.Row, s.Column)).Distinct().Count() != seats.Length)
                throw new FormatException();

            return new BusProviderDetail(service, seats);
        });
    }

    private T Parse<T>(JsonElement response, Func<JsonElement, T> read)
    {
        try
        {
            var result = response.GetProperty("Result");
            if (result.GetProperty("Type").GetString() != "Success" || result.GetProperty("Code").GetInt32() != 0)
            {
                logger.LogWarning("HiHoliday returned result code {Code}", result.GetProperty("Code").GetInt32());
                throw new BusRequestException("تأمین‌کننده درخواست اتوبوس را تأیید نکرد.", 502);
            }

            return read(response);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException or KeyNotFoundException)
        {
            logger.LogWarning("HiHoliday response could not be mapped. Error type {ErrorType}", ex.GetType().Name);
            throw new BusRequestException("پاسخ سرویس اتوبوس معتبر نیست.", 502);
        }
    }

    private static BusProviderService ReadService(JsonElement bus)
    {
        var code = bus.GetProperty("Code").GetString() ?? throw new FormatException();
        var time = bus.GetProperty("DepartureDate").GetDateTime();
        var capacity = bus.GetProperty("Capacity").GetInt32();
        if (!long.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0 || capacity < 0)
            throw new FormatException();

        return new(code, Caption(bus.GetProperty("Company"), "Name"), Caption(bus.GetProperty("Vehicle"), "Name"),
            Caption(bus.GetProperty("Departure"), "FaRegionName", "RegionName"),
            Caption(bus.GetProperty("Arrival"), "FaRegionName", "RegionName"), TimeOnly.FromDateTime(time),
            capacity, HiHolidayClient.ToRials(bus.GetProperty("Price").GetDecimal()));
    }

    private static BusProviderRegion ReadRegion(JsonElement region)
    {
        var parent = region.GetProperty("ParentRegion");
        var type = region.GetProperty("Type");
        return new(region.GetProperty("RegionID").GetInt32(), Caption(region, "FaRegionName"),
            Caption(region, "RegionName"), type.ValueKind == JsonValueKind.Object ? Caption(type, "TypeTitle") : "",
            Caption(region, "Code"), parent.ValueKind == JsonValueKind.Object ? parent.GetProperty("RegionID").GetInt32() : null,
            parent.ValueKind == JsonValueKind.Object ? Caption(parent, "FaRegionName", "RegionName") : "");
    }

    private static string Caption(JsonElement item, string field, string? fallback = null) =>
        item.TryGetProperty(field, out var value) && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!
            : fallback is not null ? Caption(item, fallback) : "";
}
