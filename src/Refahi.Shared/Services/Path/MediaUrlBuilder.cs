namespace Refahi.Shared.Services.Path;

public static class MediaUrlBuilder
{
    public static string MakeAbsolute(string loadBaseUrl, string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
            return mediaPath;

        if (
            !Uri.TryCreate(loadBaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        )
            throw new InvalidOperationException(
                "MediaStorage:LoadBaseUrl باید یک آدرس مطلق HTTP یا HTTPS باشد"
            );

        var pathAndQuery = Uri.TryCreate(mediaPath, UriKind.Absolute, out var currentUri)
            ? currentUri.PathAndQuery
            : mediaPath.Replace('\\', '/');

        var queryIndex = pathAndQuery.IndexOfAny(['?', '#']);
        var path = queryIndex >= 0 ? pathAndQuery[..queryIndex] : pathAndQuery;
        var suffix = queryIndex >= 0 ? pathAndQuery[queryIndex..] : string.Empty;

        var configuredBasePath = baseUri.AbsolutePath.TrimEnd('/');
        if (
            configuredBasePath.Length > 0
            && (
                path.Equals(configuredBasePath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(configuredBasePath + "/", StringComparison.OrdinalIgnoreCase)
            )
        )
            path = path[configuredBasePath.Length..];

        if (
            path.Equals("/api/images", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/images/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/videos", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/videos/", StringComparison.OrdinalIgnoreCase)
        )
            path = path[4..];

        return new Uri(baseUri, path.TrimStart('/') + suffix).AbsoluteUri;
    }
}
