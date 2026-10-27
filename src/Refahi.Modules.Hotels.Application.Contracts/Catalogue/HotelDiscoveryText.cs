using System.Net;
using System.Text.RegularExpressions;

namespace Refahi.Modules.Hotels.Application.Contracts.Catalogue;

public static partial class HotelDiscoveryText
{
    // Output is always rendered as text, never MarkupString/HTML.
    public static string Plain(string? value) => Whitespace().Replace(WebUtility.HtmlDecode(Tags().Replace(Scripts().Replace(value ?? "", " "), " ")), " ").Trim();
    public static string Summary(string? value)
    {
        var text = Plain(value);
        return text.Length <= 200 ? text : text[..200];
    }
    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex Scripts();
    [GeneratedRegex(@"<[^>]*>", RegexOptions.None, 1000)]
    private static partial Regex Tags();
    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex Whitespace();
}
