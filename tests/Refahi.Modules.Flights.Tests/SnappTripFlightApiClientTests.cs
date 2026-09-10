using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class SnappTripFlightApiClientTests
{
    [Fact]
    public async Task BookAsync_OmitsOptionalNullValues()
    {
        var handler = new CapturingHandler("{\"bookId\":\"book-1\"}");
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var request = new SnappTripBookRequest
        {
            FareSourceCode = "fare-1",
            PhoneNumber = "+989121234567",
            Email = "passenger@example.com",
            Passengers =
            [
                new SnappTripBookPassenger
                {
                    NationalityCode = "IR",
                    NationalId = "0154721621",
                    FirstName = "Ali",
                    LastName = "Karimi",
                    Gender = "MALE",
                    Birthday = "1990-04-01",
                    PassengerType = "ADULT",
                    PassportInfo = null,
                },
            ],
        };

        await client.BookAsync(request, CancellationToken.None);

        Assert.DoesNotContain("passportInfo", handler.Body);
        Assert.Contains("\"gender\":\"MALE\"", handler.Body);
        Assert.Contains("\"passengerType\":\"ADULT\"", handler.Body);
    }

    [Fact]
    public async Task BookAsync_OnProviderValidationError_ThrowsTypedSafeException()
    {
        var handler = new CapturingHandler(
            "{\"detail\":\"sensitive provider validation detail\"}",
            HttpStatusCode.BadRequest
        );
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<FlightProviderException>(() =>
            client.BookAsync(new SnappTripBookRequest(), CancellationToken.None)
        );

        Assert.Equal((int)HttpStatusCode.BadRequest, exception.HttpStatusCode);
        Assert.Equal("book", exception.Operation);
        Assert.DoesNotContain("sensitive", exception.Message);
    }

    [Fact]
    public async Task SearchAsync_SendsCurlCompatibleRequest()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var options = Options.Create(
            new SnappTripFlightOptions
            {
                BaseUrl = "https://b2bapiv2.snapptrip.com/flight",
                ApiBasePath = "api/v1",
                ApiKey = "test-api-key",
            }
        );
        var client = new SnappTripFlightApiClient(
            httpClient,
            NullLogger<SnappTripFlightApiClient>.Instance,
            options
        );
        var request = new SnappTripSearchRequest
        {
            Adult = 1,
            Child = 0,
            Infant = 0,
            IsDomestic = true,
            OriginDestinationInformations =
            [
                new SnappTripOriginDestinationInformation
                {
                    DepartureDate = "2026-03-26",
                    OriginLocationCode = "THR",
                    DestinationLocationCode = "KIH",
                    OriginType = "AIRPORT",
                    DestinationType = "AIRPORT",
                },
            ],
            TravelPreference = new SnappTripTravelPreference
            {
                CabinType = "ECONOMY",
                MaxStopsQuantity = "ALL",
                AirTripType = "RETURN",
            },
        };

        await client.SearchAsync(request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://b2bapiv2.snapptrip.com/flight/api/v1/search",
            handler.RequestUri?.ToString()
        );
        Assert.Contains("application/json", handler.Accept);
        Assert.Equal(["test-api-key"], handler.ApiKey);
        Assert.Equal("application/json", handler.ContentType?.MediaType);
        Assert.Null(handler.ContentType?.CharSet);
        Assert.Contains("\"maxStopsQuantity\":\"ALL\"", handler.Body);
    }

    private static SnappTripFlightApiClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(
            new SnappTripFlightOptions
            {
                BaseUrl = "https://b2bapiv2.snapptrip.com/flight",
                ApiBasePath = "api/v1",
                ApiKey = "test-api-key",
            }
        );

        return new SnappTripFlightApiClient(
            httpClient,
            NullLogger<SnappTripFlightApiClient>.Instance,
            options
        );
    }

    private sealed class CapturingHandler(
        string responseBody = "{\"success\":true}",
        HttpStatusCode responseStatusCode = HttpStatusCode.OK
    ) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public IReadOnlyList<string> Accept { get; private set; } = [];

        public IReadOnlyList<string> ApiKey { get; private set; } = [];

        public MediaTypeHeaderValue? ContentType { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Accept = request
                .Headers.Accept.Select(header => header.MediaType ?? string.Empty)
                .ToArray();
            ApiKey = request.Headers.TryGetValues("api-key", out var apiKey)
                ? apiKey.ToArray()
                : [];
            ContentType = request.Content?.Headers.ContentType;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(responseStatusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
