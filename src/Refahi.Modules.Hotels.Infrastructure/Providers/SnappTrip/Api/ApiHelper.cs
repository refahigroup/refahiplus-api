using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;
using System.Net.Http.Json;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;

public static class ApiHelper
{
    public static async Task<T> GetAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            await ThrowApiError(response, url);

        string json = await response.Content.ReadAsStringAsync();

        var result = await response.Content.ReadFromJsonAsync<T>();
        if (result == null)
            throw new Exception($"SnappTrip GET {url} returned NULL");

        return result;
    }

    public static async Task<T> PostAsync<T>(this HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
            await ThrowApiError(response, url);

        string json = await response.Content.ReadAsStringAsync();

        var result = await response.Content.ReadFromJsonAsync<T>();

        if (result == null)
            throw new Exception($"SnappTrip POST {url} returned NULL");

        return result;
    }

    public static async Task PostNoContentAsync(this HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
            await ThrowApiError(response, url);
    }

    private static async Task ThrowApiError(HttpResponseMessage response, string url)
    {
        var err = await response.Content.ReadFromJsonAsync<SnappTripApiError>();

        var message = $"SnappTrip Error calling {url}. " +
                      $"Status={(int)response.StatusCode}, " +
                      $"Code={err?.code}, " +
                      $"Message={err?.message}, Trace={err?.trace_id}";

        throw new Exception(message);
    }
}
