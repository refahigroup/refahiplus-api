using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Refahi.Modules.Flights.Infrastructure.Persistence;

internal static partial class FlightAirportSearchNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(character switch
            {
                '\u064A' or '\u0649' => '\u06CC',
                '\u0643' => '\u06A9',
                '\u200C' or '\u200D' => ' ',
                _ => char.ToLowerInvariant(character)
            });
        }

        return WhitespaceRegex().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
