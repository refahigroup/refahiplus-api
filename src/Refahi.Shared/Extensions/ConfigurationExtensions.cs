using Microsoft.Extensions.Configuration;

namespace Refahi.Shared.Extensions;

public static class ConfigurationExtensions
{
    public static string GetConnectionString(this IConfiguration configuration)
    {
        var connectionStringName = configuration.GetSection("ConnectionStringName").Value;

        if (string.IsNullOrEmpty(connectionStringName))
            throw new Exception("Invalid Connection String Name");

        var connectionString = configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrEmpty(connectionStringName))
            throw new Exception("Invalid Connection String");

        return connectionString?.ReplaceWithEnvironmentVariables() ?? string.Empty;
    }
}
