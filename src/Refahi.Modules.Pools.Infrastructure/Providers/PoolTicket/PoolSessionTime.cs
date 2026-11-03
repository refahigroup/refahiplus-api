using System.Globalization;
using System.Text.RegularExpressions;

namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;

public static partial class PoolSessionTime
{
    // Unknown provider formats remain displayable but never authorize a purchase or redemption.
    public static (DateTimeOffset? Start, DateTimeOffset? End) Parse(string date, string time)
    {
        date = Digits(date).Trim(); time = Digits(time).Trim();
        var match = Range().Match(time);
        if (!match.Success) return (null, null);
        try
        {
            var parts = date.Split('/'); DateTime day;
            if (parts.Length == 3 && int.TryParse(parts[0], out var year) && year is >= 1300 and <= 1600)
                day = new PersianCalendar().ToDateTime(year, int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
            else if (!DateTime.TryParseExact(date, ["yyyy-MM-dd", "yyyy/MM/dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return (null, null);
            if (!TimeOnly.TryParseExact(match.Groups[1].Value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                || !TimeOnly.TryParseExact(match.Groups[2].Value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
                || to <= from) return (null, null);
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date + from.ToTimeSpan(), DateTimeKind.Unspecified), zone)),
                new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date + to.ToTimeSpan(), DateTimeKind.Unspecified), zone)));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { return (null, null); }
    }
    public static (DateTimeOffset? Start, DateTimeOffset? End) ParseDateTimes(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end)) return (null, null);
        DateTimeOffset ParseOne(string? value)
        {
            var text = Digits(value ?? "").Trim();
            if (DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset))
                return offset.ToUniversalTime();
            if (DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out offset))
                return offset.ToUniversalTime();
            if (DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")));
            throw new Refahi.Modules.Pools.Application.Contracts.PoolException("زمان دریافتی تأمین‌کننده معتبر نیست", 502, "POOLS_PROVIDER_INVALID_TIME");
        }
        var from = ParseOne(start); var to = ParseOne(end);
        if (to <= from) throw new Refahi.Modules.Pools.Application.Contracts.PoolException("بازه زمانی تأمین‌کننده معتبر نیست", 502, "POOLS_PROVIDER_INVALID_TIME");
        return (from, to);
    }
    private static string Digits(string s) => string.Concat(s.Select(c => c is >= '۰' and <= '۹' ? (char)('0' + c - '۰') : c is >= '٠' and <= '٩' ? (char)('0' + c - '٠') : c));
    [GeneratedRegex(@"^(\d{1,2}:\d{2})\s*(?:تا|[-–—])\s*(\d{1,2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Range();
}
