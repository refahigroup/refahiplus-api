using Refahi.Shared.Services.Path;

namespace Refahi.Api.Services.Path;

public class PathService : IPathService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PathService> _logger;

    public PathService(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<PathService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public string MakeAbsoluteMediaUrl(string mediaPath)
    {
        if (_hostEnvironment.IsDevelopment())
            return mediaPath;
        
        string baseUrl = _configuration["MediaStorage:LoadBaseUrl"]?.ToLower() ?? "";
        string result =  new Uri( new Uri(baseUrl), mediaPath).AbsoluteUri;

        return result;
    }
}
