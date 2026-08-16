using Refahi.Shared.Services.Path;

namespace Refahi.Api.Services.Path;

public class PathService : IPathService
{
    private readonly IConfiguration _configuration;

    public PathService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string MakeAbsoluteMediaUrl(string mediaPath)
    {
        var loadBaseUrl = _configuration["MediaStorage:LoadBaseUrl"] ?? string.Empty;
        return MediaUrlBuilder.MakeAbsolute(loadBaseUrl, mediaPath);
    }
}
