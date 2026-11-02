using System.Text.RegularExpressions;
namespace Refahi.Modules.Entertainments.Application.Contracts.Discovery;

public static class EntertainmentPresentation
{
    // External branding is not customer content. Operational payloads and ticket credentials are never rewritten.
    public static string? Text(string? text) => text == null ? null : Regex.Replace(text,
        @"touris[tm]\s*panel|توریست[\s‌]*پنل|توریسم[\s‌]*پنل", "", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)).Trim();
}
