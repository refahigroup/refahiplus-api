using Microsoft.Extensions.Configuration;

namespace Refahi.Modules.Store.Application.Services;

internal sealed class StoreBusinessClock : IStoreBusinessClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public StoreBusinessClock(TimeProvider timeProvider, IConfiguration configuration)
    {
        _timeProvider = timeProvider;
        _timeZone = ResolveTimeZone(configuration["Store:BusinessTimeZoneId"]);
    }

    public StoreBusinessMoment Current
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime;
            return new StoreBusinessMoment(
                DateOnly.FromDateTime(local),
                TimeOnly.FromDateTime(local));
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string? configuredId)
    {
        var candidates = new[] { configuredId, "Asia/Tehran", "Iran Standard Time" }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate!);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Iran has used UTC+03:30 without daylight-saving changes since 2022.
        return TimeZoneInfo.CreateCustomTimeZone("Refahi-Iran", TimeSpan.FromMinutes(210), "Iran", "Iran");
    }
}
