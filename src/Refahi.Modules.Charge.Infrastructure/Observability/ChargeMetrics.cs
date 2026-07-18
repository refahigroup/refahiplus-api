using System.Diagnostics.Metrics;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Infrastructure.Observability;

internal static class ChargeMetrics
{
    private static readonly Meter Meter = new("Refahi.Charge", "1.0.0");
    private static readonly Counter<long> ProviderFailures = Meter.CreateCounter<long>("charge.provider.failures");
    private static readonly Counter<long> WorkerHeartbeats = Meter.CreateCounter<long>("charge.worker.heartbeats");
    private static readonly Histogram<int> ReconciliationBatchSize = Meter.CreateHistogram<int>("charge.reconciliation.batch_size");

    public static void ProviderFailure(string provider, string operation, ProviderCallOutcome outcome, int? statusCode) =>
        ProviderFailures.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", outcome.ToString()),
            new KeyValuePair<string, object?>("http.status_code", statusCode));

    public static void WorkerHeartbeat(string worker) =>
        WorkerHeartbeats.Add(1, new KeyValuePair<string, object?>("worker", worker));

    public static void ReconciliationBatch(int count) => ReconciliationBatchSize.Record(count);
}
