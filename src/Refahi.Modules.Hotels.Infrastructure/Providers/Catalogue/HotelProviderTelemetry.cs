using System.Diagnostics.Metrics;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

internal static class HotelProviderTelemetry
{
    private static readonly Meter Meter = new("Refahi.Hotels.Providers", "1.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("hotel.provider.requests");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("hotel.provider.duration", "ms");
    internal static void Record(string provider, string operation, string outcome, double elapsedMs)
    {
        var tags = new System.Diagnostics.TagList { { "provider", provider }, { "operation", operation }, { "outcome", outcome } };
        Requests.Add(1, tags);
        Latency.Record(elapsedMs, tags);
    }
}
