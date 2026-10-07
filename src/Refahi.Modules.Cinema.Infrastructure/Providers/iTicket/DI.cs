using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

public static class DI
{
    private const string ClientName = "iTicket";

    public static IServiceCollection AddiTicketClient(this IServiceCollection services, IConfiguration configuration /*, string apiAccessToken, Uri? baseAddress = null*/ )
    {
        services.Configure<iTicketOptions>(
            configuration.GetSection(iTicketOptions.Path)
        );

        services.AddHttpClient(ClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<iTicketOptions>>();

            client.BaseAddress = new Uri(options.Value.BaseUrl);
        });

        services.AddTransient<IiTicketClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<iTicketOptions>>();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(ClientName);

            return new iTicketClient(httpClient, options);
        });

        return services;
    }
}
