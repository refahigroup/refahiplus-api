namespace Refahi.Modules.References.Application;
internal static class ReferenceSearch
{
    internal static bool Matches(string name, string? englishName, string? query) =>
        string.IsNullOrWhiteSpace(query) || Normalize(name).Contains(Normalize(query), StringComparison.OrdinalIgnoreCase)
        || Normalize(englishName ?? string.Empty).Contains(Normalize(query), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string text) => text.Trim().Normalize(System.Text.NormalizationForm.FormKC)
        .Replace('\u064a', '\u06cc').Replace('\u0643', '\u06a9').Replace('\u200c', ' ');
}
