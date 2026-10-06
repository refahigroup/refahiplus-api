using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

public static class DI
{
    private const string ClientName = "iTicket";

    public static IServiceCollection AddiTicketClient(this IServiceCollection services, IConfiguration configuration /*, string apiAccessToken, Uri? baseAddress = null*/ )
    {
        iTiketOptions options = new iTiketOptions();

        services.AddHttpClient(ClientName, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddTransient<IiTicketClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(ClientName);

            return new iTicketClient(httpClient, options.AccessToken);
        });

        return services;
    }
}
