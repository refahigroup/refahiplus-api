using System.Net;
using System.Text;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class AabsarApiClientTests
{
    [Fact]
    public async Task Uses_documented_paths_and_distinct_showtime_property_names()
    {
        var handler = new RecordingHandler();
        var client = new AabsarApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://provider.test/api/") });

        await client.CheckShowtimeAsync(new() { ShowtimeId = "showtime" });
        await client.CreateOrderAsync(new()
        {
            Items = [new() { ShowtimeId = "01ARZ3NDEKTSV4RRFFQ69G5FAV", AdultQuantity = 1 }],
            User = new() { FullName = "کاربر تست", Phone = "9123456789" }
        });

        Assert.Equal("/api/check-showtime", handler.Requests[0].Path);
        Assert.Contains("\"showtime_id\":\"showtime\"", handler.Requests[0].Body);
        Assert.Equal("/api/order-created", handler.Requests[1].Path);
        Assert.Contains("\"showtimeId\":\"01ARZ3NDEKTSV4RRFFQ69G5FAV\"", handler.Requests[1].Body);
        Assert.DoesNotContain("showtime_id", handler.Requests[1].Body);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
            var body = request.RequestUri.AbsolutePath.EndsWith("check-showtime", StringComparison.Ordinal)
                ? "{\"statusCode\":200,\"data\":{\"id\":\"showtime\",\"capacity\":5,\"status\":\"active\"}}"
                : "{\"statusCode\":200,\"data\":{\"order_code\":\"order\",\"total_price\":1,\"tickets\":[]}}";
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
