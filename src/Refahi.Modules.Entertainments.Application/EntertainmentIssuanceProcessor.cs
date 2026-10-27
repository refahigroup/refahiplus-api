using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Entertainments.Application;

public sealed class EntertainmentIssuanceProcessor(IEntertainmentRepository repository, IEntertainmentProviderFactory providers,
    IEntertainmentSecretProtector secrets, IMediator mediator, ILogger<EntertainmentIssuanceProcessor> logger,
    IOptions<EntertainmentRuntimeOptions> options, IEntertainmentMutationLock gate, IEntertainmentSessionRepository sessions, IEntertainmentPrivateDataRetention retention)
{
    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        foreach (var candidate in await repository.GetAwaitingPaymentAsync(25, ct))
        {
            var central = await mediator.Send(new Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderByIdQuery(candidate.OrderId!.Value, candidate.UserId, "User"), ct);
            if (central is null || central.SourceModule != "Entertainments" || central.SourceReferenceId != candidate.Id || central.ReferenceType != "EntertainmentPurchase" || central.Items.Sum(x => checked(x.UnitPriceMinor * x.Quantity)) != candidate.TotalAmountMinor) continue;
            await using var held = await gate.AcquireAsync(candidate.Id, ct);
            var value = await repository.GetFreshOrderAsync(candidate.Id, ct);
            if (value is null) continue;
            value.CheckedPayment();
            if (central.PaymentState == "Paid") value.QueueFulfillment();
            // The payment event normally supplies PaymentId; Orders is the fallback authority.
            if (central.Status is "Cancelled" or "Refunded") value.MarkCancelled(central.PaymentState == "Refunded");
            await repository.SaveChangesAsync(ct);
        }
        var pending = await repository.GetPendingFulfillmentAsync(10, ct);
        foreach (var candidate in pending)
        {
            bool compensate;
            await using (await gate.AcquireAsync(candidate.Id, ct))
            {
                var order = await repository.GetFreshOrderAsync(candidate.Id, ct);
                if (order == null) continue;
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                { ["EntertainmentPurchaseId"] = order.Id, ["OrderId"] = order.OrderId, ["PaymentId"] = order.PaymentId });
                compensate = await ProcessAsync(order, ct);
            }
            // Never acquire the Orders lock while holding the Entertainment lock. Cancellation calls Entertainment back.
            if (compensate && candidate.OrderId.HasValue)
            {
                try { await mediator.Send(new CancelOrderCommand(candidate.OrderId.Value, "جبران شکست صدور", $"entertainments-compensation:{candidate.Id:N}", CallerRole: "System"), ct); }
                catch (Exception ex) { logger.LogError("Entertainment compensation pending. Order={Order}, ErrorType={ErrorType}", candidate.Id, ex.GetType().Name); }
            }
        }
        foreach (var session in await sessions.GetExpiredAsync(ct))
        {
            try { await mediator.Send(new Features.Checkout.Sessions.ReleaseEntertainmentSessionCommand(session.UserId, session.Id), ct); }
            catch (Exception ex) { logger.LogWarning("Entertainment reservation cleanup pending. Session={Session}, ErrorType={ErrorType}", session.Id, ex.GetType().Name); }
        }
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(options.Value.RecipientRetentionDays, 1, 3650));
        foreach (var order in await repository.GetOrdersForRecipientRedactionAsync(cutoff, 100, ct)) order.RedactRecipient();
        foreach (var session in await sessions.GetForRedactionAsync(cutoff, ct)) session.RedactPrivateData();
        await repository.SaveChangesAsync(ct);
        await retention.PruneAsync(cutoff, ct);
    }

    private async Task<bool> ProcessAsync(EntertainmentPurchase order, CancellationToken ct)
    {
        if (order.Status == EntertainmentPurchaseStatus.CompensationPending) return true;
        if (order.Status is not (EntertainmentPurchaseStatus.IssuancePending or EntertainmentPurchaseStatus.Issuing or EntertainmentPurchaseStatus.AwaitingResult)) return false;
        if (!order.OrderId.HasValue)
        { order.RequireManualReview(); await repository.SaveChangesAsync(ct); return false; }
        order.BeginFulfillment(); await repository.SaveChangesAsync(ct);
        foreach (var fulfillment in order.Fulfillments.OrderBy(x => x.ProviderKey))
        {
            if (fulfillment.Status is ProviderFulfillmentStatus.Completed or ProviderFulfillmentStatus.Cancelled) continue;
            var provider = providers.GetRequired(fulfillment.ProviderKey);
            var operation = $"entertainments-fulfill:{fulfillment.Id:N}";
            var prior = fulfillment.Attempts.LastOrDefault(x => x.Operation == "fulfill");
            var recovering = prior != null;
            ProviderOperationAttempt? attempt = null;
            try
            {
                EntertainmentFulfillmentResult result;
                if (recovering)
                {
                    if (provider is not IEntertainmentFulfillmentStatusProvider status
                        || fulfillment.ReconcileStartedAt < DateTimeOffset.UtcNow.AddMinutes(-options.Value.ReconciliationMinutes))
                    { order.RequireManualReview(); fulfillment.Fail("نتیجه صدور نیازمند بررسی است", true); await repository.SaveChangesAsync(ct); return false; }
                    result = await status.GetStatusAsync(operation, fulfillment.ProviderInvoiceId, ct);
                }
                else
                {
                    var lines = order.Items.Where(x => x.ProviderKey.Equals(fulfillment.ProviderKey, StringComparison.OrdinalIgnoreCase))
                        .Select(x => new EntertainmentFulfillmentLine(x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.UnitPriceMinor, x.ProviderPayloadJson)).ToArray();
                    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lines))));
                    attempt = fulfillment.BeginAttempt("fulfill", operation, hash);
                    await repository.SaveChangesAsync(ct);
                    result = await provider.FulfillAsync(new(operation, secrets.Unprotect(order.RecipientNameProtected), secrets.Unprotect(order.RecipientMobileProtected), lines)
                    { ReservationReference = order.ReservationReference, ReservationContext = order.ReservationContextProtected }, ct);
                }
                attempt?.Complete();
                if (recovering) prior?.Complete();
                if (result.Status == "Failed")
                {
                    fulfillment.Fail("عدم صدور قطعی اعلام شد", false); order.BeginCompensation();
                    await repository.SaveChangesAsync(ct); return true;
                }
                if (result.Status == "ManualReview")
                {
                    fulfillment.AwaitResult(result.ProviderInvoiceId, result.ProviderPaymentId);
                    fulfillment.Fail("نتیجه صدور نیازمند بررسی است", true); order.RequireManualReview();
                    await repository.SaveChangesAsync(ct); return false;
                }
                if (result.Status == "Pending")
                {
                    fulfillment.AwaitResult(result.ProviderInvoiceId, result.ProviderPaymentId); order.AwaitResult();
                    await repository.SaveChangesAsync(ct); return false;
                }
                if (result.Status != "Completed" || result.Tickets.Count != order.Items.Where(x => x.ProviderKey == fulfillment.ProviderKey).Sum(x => x.Quantity)
                    || result.Tickets.Any(x => string.IsNullOrWhiteSpace(x.Code)))
                    throw new EntertainmentProviderAmbiguousException("بلیط‌های برگشتی کامل نیست");
                fulfillment.SetDelivery(secrets.Protect(JsonSerializer.Serialize(result)), result.ProviderInvoiceId, result.ProviderPaymentId);
                fulfillment.Complete(result.ProviderOrderCode, result.Tickets.Select(x => (secrets.Hash(fulfillment.ProviderKey + ":" + (x.ProviderTicketId ?? x.Code)), secrets.Protect(x.Code), x.IsChild)));
                await repository.SaveChangesAsync(ct);
                logger.LogInformation("Entertainment fulfillment completed. Provider={Provider}, Fulfillment={Fulfillment}", fulfillment.ProviderKey, fulfillment.Id);
            }
            catch (Exception ex)
            {
                // A cancelled process, malformed response, or interrupted commit is never proof of non-issuance.
                var ambiguous = recovering || provider is IEntertainmentReservationProvider || ex is EntertainmentProviderAmbiguousException or OperationCanceledException or HttpRequestException;
                attempt?.Fail(ambiguous ? "نتیجه عملیات نامشخص است" : "عملیات رد شد", ambiguous);
                if (ambiguous && provider is IEntertainmentFulfillmentStatusProvider)
                { fulfillment.AwaitResult(null, null); order.AwaitResult(); }
                else if (ambiguous)
                { fulfillment.Fail("نتیجه عملیات نیازمند بررسی است", true); order.RequireManualReview(); }
                else
                { fulfillment.Fail("عملیات ناموفق بود", false); order.BeginCompensation(); }
                await repository.SaveChangesAsync(CancellationToken.None);
                logger.LogWarning("Entertainment operation incomplete. Provider={Provider}, ErrorType={ErrorType}", fulfillment.ProviderKey, ex.GetType().Name);
                return !ambiguous;
            }
        }
        order.Complete(); await repository.SaveChangesAsync(ct); return false;
    }
}

public sealed class EntertainmentRuntimeOptions
{
    public const string SectionName = "Entertainments";
    public int RecipientRetentionDays { get; init; } = 90;
    public int ReconciliationMinutes { get; init; } = 30;
}
public sealed class ProcessEntertainmentFulfillmentBatchHandler(EntertainmentIssuanceProcessor processor) : IRequestHandler<ProcessEntertainmentFulfillmentBatchCommand>
{
    public async Task<Unit> Handle(ProcessEntertainmentFulfillmentBatchCommand request, CancellationToken ct)
    { await processor.ProcessBatchAsync(ct); return Unit.Value; }
}
