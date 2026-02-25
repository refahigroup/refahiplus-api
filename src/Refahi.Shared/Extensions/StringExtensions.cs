using System.Text.RegularExpressions;

namespace Refahi.Shared.Extensions;

public static class StringExtensions
{
    private static readonly Regex _pattern = new(@"\{(?<key>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    public static string ReplaceWithEnvironmentVariables(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return _pattern.Replace(input, match =>
        {
            var key = match.Groups["key"].Value;
            var value = Environment.GetEnvironmentVariable(key);

            return value ?? match.Value;
        });
    }
}
