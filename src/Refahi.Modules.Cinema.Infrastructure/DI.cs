using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public static class ITicketServiceCollectionExtensions
{
    private const string ClientName = "iTicket";

    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddITicketClient(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        string apiAccessToken,
        Uri? baseAddress = null)
    {
        services.AddHttpClient(ClientName, client =>
        {
            client.BaseAddress = baseAddress ?? new Uri("https://console.iticket.ir/api/v1/");
        });

        services.AddTransient<IITicketClient>(sp =>
        {
            var factory = sp.GetRequiredService<Microsoft.Extensions.Http.IHttpClientFactory>();
            var httpClient = factory.CreateClient(ClientName);
            return new ITicketClient(httpClient, apiAccessToken);
        });

        return services;
    }
}
