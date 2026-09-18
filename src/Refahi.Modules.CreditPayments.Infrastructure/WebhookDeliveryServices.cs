using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed record ValidatedWebhookDestination(Uri Uri, IReadOnlyList<IPAddress> Addresses);
public sealed record WebhookTransportRequest(Uri Uri, IReadOnlyList<IPAddress> Addresses,
    byte[] Body, IReadOnlyDictionary<string, string> Headers, TimeSpan Timeout);
public sealed record WebhookTransportResult(int StatusCode, TimeSpan? RetryAfter);

public interface IWebhookDnsResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct);
}

public interface IWebhookEndpointSecurityValidator
{
    Task<ValidatedWebhookDestination> ValidateAsync(string url, CancellationToken ct);
}

public interface IWebhookHttpTransport
{
    Task<WebhookTransportResult> SendAsync(WebhookTransportRequest request, CancellationToken ct);
}

public sealed class SystemWebhookDnsResolver : IWebhookDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var literal)) return [literal];
        return await Dns.GetHostAddressesAsync(host, ct);
    }
}

public sealed class WebhookEndpointSecurityValidator(IWebhookDnsResolver dns,
    IOptions<WebhookOptions> configured) : IWebhookEndpointSecurityValidator
{
    public async Task<ValidatedWebhookDestination> ValidateAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new WebhookSecurityException("endpoint_url_invalid");
        var host = uri.IdnHost;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new WebhookSecurityException("endpoint_localhost_blocked");
        var port = uri.IsDefaultPort ? 443 : uri.Port;
        var allowed = configured.Value.AllowedPorts is { Length: > 0 }
            ? configured.Value.AllowedPorts : [443];
        if (!allowed.Contains(port)) throw new WebhookSecurityException("endpoint_port_blocked");
        IReadOnlyList<IPAddress> addresses;
        try { addresses = await dns.ResolveAsync(host, ct); }
        catch (SocketException) { throw new WebhookSecurityException("endpoint_dns_failure"); }
        if (addresses.Count == 0) throw new WebhookSecurityException("endpoint_dns_empty");
        if (addresses.Any(x => !WebhookIpPolicy.IsPublic(x)))
            throw new WebhookSecurityException("endpoint_address_blocked");
        return new(uri, addresses.Distinct().ToArray());
    }
}

public sealed class WebhookSecurityException(string failureCode) : Exception(failureCode)
{
    public string FailureCode { get; } = failureCode;
}

public static class WebhookIpPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 ||
                b[0] == 100 && b[1] is >= 64 and <= 127 ||
                b[0] == 169 && b[1] == 254 ||
                b[0] == 172 && b[1] is >= 16 and <= 31 ||
                b[0] == 192 && b[1] == 0 && b[2] is 0 or 2 ||
                b[0] == 192 && b[1] == 168 ||
                b[0] == 198 && b[1] is 18 or 19 ||
                b[0] == 198 && b[1] == 51 && b[2] == 100 ||
                b[0] == 203 && b[1] == 0 && b[2] == 113 ||
                b[0] >= 224);
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return false;
        var bytes = address.GetAddressBytes();
        if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6Loopback) ||
            address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            return false;
        if ((bytes[0] & 0xfe) == 0xfc) return false; // fc00::/7
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
            return false; // documentation
        return true;
    }
}

public sealed class PinnedWebhookHttpTransport : IWebhookHttpTransport
{
    public async Task<WebhookTransportResult> SendAsync(WebhookTransportRequest request, CancellationToken ct)
    {
        var addresses = request.Addresses.ToArray();
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, token) =>
            {
                Exception? last = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                    {
                        last = ex; socket.Dispose();
                        if (ex is OperationCanceledException) throw;
                    }
                }
                throw last ?? new SocketException((int)SocketError.HostUnreachable);
            }
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var message = new HttpRequestMessage(HttpMethod.Post, request.Uri)
        {
            Content = new ByteArrayContent(request.Body)
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.UserAgent.ParseAdd("Refahi-CreditPayments-Webhook/1.0");
        foreach (var header in request.Headers) message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(request.Timeout);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
            retryAfter = date - DateTimeOffset.UtcNow;
        return new((int)response.StatusCode, retryAfter > TimeSpan.Zero ? retryAfter : null);
    }
}

public static class WebhookSignature
{
    public static string Create(string secret, Guid eventId, long unixTimestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{eventId:D}.{unixTimestamp.ToString(CultureInfo.InvariantCulture)}.");
        var input = new byte[prefix.Length + body.Length];
        prefix.CopyTo(input, 0); body.CopyTo(input.AsSpan(prefix.Length));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input);
        return $"v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

public sealed record WebhookDeliveryWork(Guid DeliveryId, WebhookEvent Event,
    WebhookEndpoint? Endpoint, int AttemptCount);

public sealed class WebhookWorkService(CreditPaymentsDbContext db)
{
    public async Task<int> DispatchAsync(string owner, DateTimeOffset now, DateTimeOffset leaseUntil,
        int size, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.OutboxMessages.FromSqlInterpolated($@"SELECT * FROM credit_payments.outbox_messages
            WHERE status IN (1,2) AND (next_retry_at IS NULL OR next_retry_at <= {now})
              AND (lease_until IS NULL OR lease_until <= {now})
            ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        foreach (var row in rows)
        {
            row.Claim(owner, leaseUntil);
            if (!await db.WebhookDeliveries.AnyAsync(x => x.WebhookEventId == row.WebhookEventId, ct))
                db.WebhookDeliveries.Add(WebhookDelivery.Create(row.WebhookEventId, now));
            row.Complete(now);
            db.AuditRecords.Add(AuditRecord.Create("webhook-outbox.dispatched", "OutboxMessage", row.Id, null, now));
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows.Count;
    }

    public async Task<IReadOnlyList<Guid>> LeaseDeliveriesAsync(string owner, DateTimeOffset now,
        DateTimeOffset leaseUntil, int size, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.WebhookDeliveries.FromSqlInterpolated($@"SELECT * FROM credit_payments.webhook_deliveries
            WHERE status IN (1,2,3) AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
              AND (lease_until IS NULL OR lease_until <= {now})
            ORDER BY COALESCE(next_attempt_at,created_at),created_at
            FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        foreach (var row in rows) row.Claim(owner, now, leaseUntil);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows.Select(x => x.Id).ToArray();
    }

    public async Task<WebhookDeliveryWork?> GetWorkAsync(Guid deliveryId, CancellationToken ct)
    {
        var delivery = await db.WebhookDeliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deliveryId, ct);
        if (delivery is null || delivery.Status != WebhookDeliveryStatus.Processing) return null;
        var webhookEvent = await db.WebhookEvents.AsNoTracking().SingleAsync(x => x.Id == delivery.WebhookEventId, ct);
        var endpoint = await db.Set<WebhookEndpoint>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TerminalId == webhookEvent.TerminalId, ct);
        return new(delivery.Id, webhookEvent, endpoint, delivery.AttemptCount);
    }

    public async Task CompleteSuccessAsync(Guid deliveryId, string owner, int statusCode,
        int secretVersion, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await LockDelivery(deliveryId, ct);
        if (row.Status == WebhookDeliveryStatus.Processing && row.LeaseOwner == owner)
        {
            row.MarkDelivered(statusCode, secretVersion, now);
            db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.succeeded", "WebhookDelivery", row.Id, null, now));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task CompleteFailureAsync(Guid deliveryId, string owner, string failureCode,
        int? statusCode, int? secretVersion, DateTimeOffset now, DateTimeOffset nextAttempt,
        int maximumAttempts, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await LockDelivery(deliveryId, ct);
        if (row.Status == WebhookDeliveryStatus.Processing && row.LeaseOwner == owner)
        {
            if (row.AttemptCount >= maximumAttempts)
            {
                row.DeadLetter(failureCode, statusCode, secretVersion, now);
                db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.dead-lettered", "WebhookDelivery", row.Id, null, now));
            }
            else
            {
                row.ScheduleRetry(failureCode, statusCode, secretVersion, nextAttempt, now);
                db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.retry-scheduled", "WebhookDelivery", row.Id, null, now));
            }
            if (failureCode.StartsWith("endpoint_", StringComparison.Ordinal))
                db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.unsafe-endpoint-blocked", "WebhookDelivery", row.Id, null, now));
            if (failureCode == "secret_unprotect_failed")
                db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.secret-failure", "WebhookDelivery", row.Id, null, now));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private Task<WebhookDelivery> LockDelivery(Guid id, CancellationToken ct) =>
        db.WebhookDeliveries.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.webhook_deliveries WHERE webhook_delivery_id={id} FOR UPDATE")
            .SingleAsync(ct);
}

public sealed class WebhookOutboxDispatcherWorker(IServiceScopeFactory scopes,
    IOptions<WebhookOptions> configured, TimeProvider clock,
    ILogger<WebhookOutboxDispatcherWorker> logger) : BackgroundService
{
    private readonly string owner = $"{Environment.MachineName}:webhook-outbox:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, configured.Value.OutboxPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var work = scope.ServiceProvider.GetRequiredService<WebhookWorkService>();
                var now = clock.GetUtcNow();
                await work.DispatchAsync(owner, now, now.AddSeconds(Math.Max(10, configured.Value.LeaseSeconds)),
                    Math.Clamp(configured.Value.BatchSize, 1, 500), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Webhook outbox dispatch failed."); }
        }
    }
}

public sealed class WebhookDeliveryWorker(IServiceScopeFactory scopes, IOptions<WebhookOptions> configured,
    IWebhookEndpointSecurityValidator security, IWebhookHttpTransport transport,
    IWebhookSecretProtector secrets, TimeProvider clock, ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    private readonly string owner = $"{Environment.MachineName}:webhook-delivery:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, configured.Value.DeliveryPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var work = scope.ServiceProvider.GetRequiredService<WebhookWorkService>();
                var now = clock.GetUtcNow();
                var ids = await work.LeaseDeliveriesAsync(owner, now,
                    now.AddSeconds(Math.Max(15, configured.Value.LeaseSeconds)),
                    Math.Clamp(configured.Value.BatchSize, 1, 500), stoppingToken);
                foreach (var id in ids) await DeliverAsync(work, id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Webhook delivery batch failed."); }
        }
    }

    private async Task DeliverAsync(WebhookWorkService work, Guid deliveryId, CancellationToken ct)
    {
        var item = await work.GetWorkAsync(deliveryId, ct); if (item is null) return;
        var now = clock.GetUtcNow(); int? secretVersion = item.Endpoint?.SigningSecretVersion;
        try
        {
            if (item.Endpoint is null) throw new DeliveryFailureException("endpoint_not_configured");
            if (item.Endpoint.Status != WebhookEndpointStatus.Active) throw new DeliveryFailureException("endpoint_disabled");
            var destination = await security.ValidateAsync(item.Endpoint.Url, ct);
            if (!secrets.TryUnprotect(item.Endpoint.SigningSecretCiphertext, out var secret))
                throw new DeliveryFailureException("secret_unprotect_failed");
            var body = Encoding.UTF8.GetBytes(item.Event.PayloadJson);
            var timestamp = now.ToUnixTimeSeconds();
            var headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["X-Refahi-Webhook-Event-Id"] = item.Event.Id.ToString("D"),
                ["X-Refahi-Webhook-Event-Type"] = item.Event.EventType,
                ["X-Refahi-Webhook-Timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
                ["X-Refahi-Webhook-Signature"] = WebhookSignature.Create(secret, item.Event.Id, timestamp, body),
                ["X-Refahi-Webhook-Version"] = item.Event.EventVersion.ToString(CultureInfo.InvariantCulture)
            };
            var result = await transport.SendAsync(new(destination.Uri, destination.Addresses, body, headers,
                TimeSpan.FromSeconds(Math.Clamp(configured.Value.TimeoutSeconds, 1, 60))), ct);
            if (result.StatusCode is >= 200 and <= 299)
            {
                await work.CompleteSuccessAsync(deliveryId, owner, result.StatusCode,
                    item.Endpoint.SigningSecretVersion, clock.GetUtcNow(), ct); return;
            }
            await Fail(work, item, $"http_{result.StatusCode}", result.StatusCode,
                secretVersion, result.RetryAfter, ct);
        }
        catch (WebhookSecurityException ex) { await Fail(work, item, ex.FailureCode, null, secretVersion, null, ct); }
        catch (DeliveryFailureException ex) { await Fail(work, item, ex.FailureCode, null, secretVersion, null, ct); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { await Fail(work, item, "timeout", null, secretVersion, null, CancellationToken.None); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook delivery attempt failed. WebhookEventId={WebhookEventId} WebhookDeliveryId={WebhookDeliveryId}", item.Event.Id, item.DeliveryId);
            await Fail(work, item, "network_error", null, secretVersion, null, CancellationToken.None);
        }
    }

    private async Task Fail(WebhookWorkService work, WebhookDeliveryWork item, string code,
        int? status, int? secretVersion, TimeSpan? retryAfter, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var maximum = Math.Max(1, configured.Value.MaximumAttempts);
        var seconds = Math.Min(Math.Max(1, configured.Value.MaximumRetrySeconds),
            Math.Max(1, configured.Value.BaseRetrySeconds) * Math.Pow(2, Math.Min(20, item.AttemptCount - 1)));
        seconds += RandomNumberGenerator.GetInt32(0, Math.Max(2, (int)Math.Min(60, seconds / 4 + 1)));
        if (retryAfter is { } remote)
            seconds = Math.Min(Math.Max(seconds, remote.TotalSeconds), Math.Max(1, configured.Value.MaximumRetrySeconds));
        await work.CompleteFailureAsync(item.DeliveryId, owner, code, status, secretVersion,
            now, now.AddSeconds(seconds), maximum, ct);
    }

    private sealed class DeliveryFailureException(string code) : Exception(code)
    { public string FailureCode { get; } = code; }
}
