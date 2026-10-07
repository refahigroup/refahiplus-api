using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

public static class CinemaHtml
{
    public static string Sanitize(string? html)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(["p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "blockquote"]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);
        sanitizer.AllowDataAttributes = false;
        return sanitizer.Sanitize(html ?? "");
    }

    public static string PlainText(string? html)
    {
        var document = new HtmlParser().ParseDocument(Sanitize(html));
        foreach (var element in document.QuerySelectorAll("br, p, li, blockquote"))
            element.AppendChild(document.CreateTextNode(" "));
        return string.Join(" ", (document.Body?.TextContent ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
