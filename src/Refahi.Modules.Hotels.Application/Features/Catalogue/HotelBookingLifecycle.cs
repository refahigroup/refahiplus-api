using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed partial class HotelCatalogueService
{
    private bool CanProceed(HotelRequest request, HotelExternalReservation? reservation) =>
        request.Status is HotelRequestStatus.Created or HotelRequestStatus.ConvertedToOrder && request.ExpireAt > DateTime.UtcNow
        && (reservation is null ? request.ProviderName != "Lamasoo" : reservation.State == HotelExternalState.Reserved
            && !reservation.PaymentBlocked && !reservation.NoExternalLiability
            && reservation.SupplyPriceMinor <= request.TotalPrice
            && reservation.LockedUntilUtc > DateTime.UtcNow.AddSeconds(Policy.PaymentSafetySeconds));

    private string PublicStatus(HotelRequest request, HotelExternalReservation? reservation, string payment)
    {
        if (payment == "Refunded") return "Refunded";
        if (payment != "Paid" && reservation?.NoExternalLiability == true) return "Failed";
        if (reservation?.State == HotelExternalState.Confirmed || reservation is null && request.Status is HotelRequestStatus.ProviderConfirmed or HotelRequestStatus.Completed)
            return payment == "Paid" ? "Confirmed" : "NeedsReview";
        if (payment != "Paid" && request.ExpireAt <= DateTime.UtcNow && reservation?.ReviewReason == "LOCK_EXPIRED_NEEDS_RECONCILIATION") return "Expired";
        if (reservation?.State == HotelExternalState.Unknown || reservation?.PaymentBlocked == true) return "NeedsReview";
        if (payment == "Paid")
            return reservation?.State is HotelExternalState.Failed or HotelExternalState.Cancelled or HotelExternalState.Expired
                || reservation?.LockedUntilUtc <= DateTime.UtcNow ? "NeedsReview" : "Finalizing";
        if (reservation?.State is HotelExternalState.Failed or HotelExternalState.Cancelled || request.Status is HotelRequestStatus.Failed or HotelRequestStatus.Cancelled) return "Failed";
        if (reservation?.State == HotelExternalState.Expired || request.ExpireAt <= DateTime.UtcNow || request.Status == HotelRequestStatus.Expired) return "Expired";
        return CanProceed(request, reservation) ? "AwaitingPayment" : "Preparing";
    }

    public async Task<HotelBookingStatusDto> StatusAsync(GetHotelBookingStatusQuery query, CancellationToken ct)
    {
        var request = query.IsAdmin ? await requests.GetAsync(query.RequestId, ct) : await requests.GetForUserAsync(query.RequestId, query.UserId, ct);
        if (request is null) throw new KeyNotFoundException("درخواست هتل یافت نشد.");
        var reservation = await repository.ReservationAsync(request.Id, ct);
        var order = request.OrderId is Guid orderId ? await mediator.Send(new GetOrderByIdQuery(orderId, request.UserId, "User"), ct) : null;
        var payment = order?.PaymentState ?? "Unpaid";
        return new(request.Id, request.OrderId, payment, PublicStatus(request, reservation, payment),
            payment != "Paid" && payment != "Refunded" && (order is null || order.CanPay) && CanProceed(request, reservation), request.ExpireAt,
            reservation?.UpdatedAtUtc ?? request.UpdatedAt,
            payment == "Paid" && PublicStatus(request, reservation, payment) == "Confirmed" ? reservation?.ReservationId ?? request.ProviderBookingCode : null);
    }

    public async Task RecoverAsync(CancellationToken ct, bool reconciliationOnly = false)
    {
        var paid = new List<(Guid OrderId, Guid UserId)>();
        var refunds = new List<(Guid OrderId, Guid RequestId)>();
        foreach (var candidate in await repository.RecoveryAsync(reconciliationOnly, ct))
        {
            ct.ThrowIfCancellationRequested();
            // Every row gets a fresh read under the same lock used by payment/finalization.
            await using var held = await repository.LockAsync("reservation:" + candidate.HotelRequestId, ct);
            var reservation = await repository.ReservationAsync(candidate.HotelRequestId, ct);
            var request = await requests.GetAsync(candidate.HotelRequestId, ct);
            if (reservation is null || request is null || reservation.NextRecoveryAtUtc > DateTime.UtcNow) continue;
            var order = request.OrderId is Guid id ? await mediator.Send(new GetOrderByIdQuery(id, request.UserId, "User"), ct)
                : await mediator.Send(new GetOrderByIdempotencyKeyQuery($"hotel-request-order-{request.Id:N}", request.UserId, "Hotel"), ct);
            if (!request.OrderId.HasValue && order is not null)
            {
                if (order.UserId != request.UserId || order.SourceModule != "Hotel" || order.SourceReferenceId != request.Id
                    || order.ReferenceType != "HotelRequest" || order.FinalAmountMinor != request.TotalPrice)
                    throw new InvalidOperationException("ارتباط سفارش بازیابی‌شده معتبر نیست.");
                request.RecoverExistingOrderLink(order.Id, DateTime.UtcNow);
            }
            var isPaid = order?.PaymentState == "Paid" && order.SourceModule == "Hotel" && order.ReferenceType == "HotelRequest"
                && order.SourceReferenceId == request.Id && order.FinalAmountMinor == request.TotalPrice;
            logger.LogInformation("Hotel recovery candidate RequestId={RequestId} OrderId={OrderId} Provider={Provider} State={State} IsPaid={IsPaid} AgeSeconds={AgeSeconds} Lane={Lane}",
                request.Id, request.OrderId, reservation.Provider, reservation.State, isPaid,
                (DateTime.UtcNow - reservation.UpdatedAtUtc).TotalSeconds, reconciliationOnly ? "inquiry" : "payment");
            reservation.ScheduleRecovery(Policy.RecoveryIntervalSeconds);

            if (reconciliationOnly && reservation.State == HotelExternalState.Unknown)
                await ReconcileAsync(reservation, request, isPaid, ct);
            else if (!reconciliationOnly && reservation.State == HotelExternalState.Prepared)
                reservation.MarkUnknown("UNSENT_REQUEST_REQUIRES_REVIEW");
            else if (!reconciliationOnly && reservation.State is HotelExternalState.Submitting or HotelExternalState.Confirming)
            {
                var deadline = reservation.State == HotelExternalState.Submitting ? Policy.SubmitTimeoutSeconds : Policy.ConfirmTimeoutSeconds;
                if (reservation.UpdatedAtUtc < DateTime.UtcNow.AddSeconds(-deadline - 15)) reservation.MarkUnknown("ABANDONED_OPERATION");
                foreach (var attempt in await repository.OperationsAsync(request.Id, ct))
                    if (attempt.State == HotelExternalOperationState.Started && attempt.StartedAtUtc < DateTime.UtcNow.AddSeconds(-deadline - 15))
                        attempt.Finish(HotelExternalOperationState.Unknown, errorCode: "ABANDONED_OPERATION");
            }
            if (reservation.State == HotelExternalState.Reserved)
            {
                if (reservation.PaymentBlocked) reservation.MarkUnknown(reservation.ReviewReason);
                else if (reservation.LockedUntilUtc <= DateTime.UtcNow)
                {
                    reservation.MarkUnknown("LOCK_EXPIRED_NEEDS_RECONCILIATION");
                }
                else if (isPaid && !reservation.PaymentBlocked) paid.Add((order!.Id, request.UserId));
            }
            if (reservation.NoExternalLiability && reservation.State is HotelExternalState.Failed or HotelExternalState.Cancelled)
            {
                if (isPaid) refunds.Add((order!.Id, request.Id));
                else
                {
                    if (order?.PaymentState == "Refunded")
                    {
                        var saga = await sagas.GetByHotelRequestIdAsync(request.Id, ct);
                        saga?.Compensate("Orders confirmed original-allocation refund.", DateTime.UtcNow);
                    }
                    reservation.StopRecovery();
                }
            }
            await repository.SaveAsync(ct);
        }
        // Orders acquires its own lock before acquiring the source lock; never reverse that order here.
        foreach (var item in paid)
        {
            try { await FinalizeAsync(item.OrderId, item.UserId, ct); }
            catch (HotelExternalOutcomeUnknownException) { }
        }
        foreach (var item in refunds)
        {
            var result = await mediator.Send(new CancelOrderCommand(item.OrderId, "رزرو خارجی به‌صورت قطعی قابل انجام نیست.",
                $"hotel-external-compensation-{item.RequestId:N}", CallerRole: "System"), ct);
            if (result.PaymentAction == "Refunded")
            {
                await using var held = await repository.LockAsync("reservation:" + item.RequestId, ct);
                var saga = await sagas.GetByHotelRequestIdAsync(item.RequestId, ct);
                saga?.Compensate("External liability resolved; Orders confirmed original-allocation refund.", DateTime.UtcNow);
                await repository.SaveAsync(ct);
            }
        }
    }

    private async Task ReconcileAsync(HotelExternalReservation reservation, HotelRequest request, bool isPaid, CancellationToken ct)
    {
        var provider = providers.Get(reservation.Provider);
        if (!provider.Capabilities.Inquiry)
        {
            reservation.ScheduleRecovery(300);
            logger.LogWarning("Hotel booking needs operator reconciliation RequestId={RequestId} ReferenceId={ReferenceId} Reason={Reason}",
                request.Id, reservation.ReferenceId, reservation.ReviewReason);
            return;
        }
        var attempt = HotelExternalOperation.Start(reservation, HotelExternalOperationKind.Inquiry);
        repository.AddOperation(attempt); await repository.SaveAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(Policy.InquiryTimeoutSeconds));
        HotelInquiryResult result;
        try { result = await provider.InquireAsync(reservation.ReferenceId, deadline.Token); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            attempt.Finish(HotelExternalOperationState.Unknown, errorCode: "INQUIRY_UNKNOWN");
            reservation.ScheduleRecovery(60);
            logger.LogWarning("Hotel inquiry incomplete RequestId={RequestId} ErrorType={ErrorType}", request.Id, ex.GetType().Name);
            return;
        }
        if (result.Snapshot is not null)
        {
            try { using var evidence = System.Text.Json.JsonDocument.Parse(result.Snapshot); }
            catch (System.Text.Json.JsonException) { attempt.Finish(HotelExternalOperationState.Unknown, errorCode: "INQUIRY_MALFORMED"); return; }
        }
        attempt.Finish(HotelExternalOperationState.Succeeded, result.Snapshot);
        if (result.State == HotelInquiryState.Unsupported) { reservation.ScheduleRecovery(300); return; }
        if (!result.OperationCompleted || string.IsNullOrWhiteSpace(result.Snapshot)) return;
        foreach (var previous in await repository.OperationsAsync(request.Id, ct))
            if (previous.State == HotelExternalOperationState.Started)
                previous.Finish(HotelExternalOperationState.Unknown, errorCode: "RECOVERED_BY_INQUIRY");
        if (result.State == HotelInquiryState.Confirmed && isPaid && !string.IsNullOrWhiteSpace(result.ReservationId))
        {
            var saga = await sagas.GetByHotelRequestIdAsync(request.Id, ct);
            if (request.Status is not (HotelRequestStatus.ConvertedToOrder or HotelRequestStatus.ProviderConfirmed or HotelRequestStatus.Completed)
                || saga?.Status is HotelBookingSagaStatus.Failed or HotelBookingSagaStatus.Compensated)
            { reservation.BlockPayment("LOCAL_STATE_REQUIRES_REVIEW"); return; }
            reservation.ReconcileConfirmed(result.ReservationId, result.Snapshot);
            request.MarkProviderConfirmed(result.ReservationId, DateTime.UtcNow); request.Complete(DateTime.UtcNow);
            if (saga is not null)
            {
                saga.MarkPaid(request.OrderId!.Value, DateTime.UtcNow); saga.MarkProviderBookingStarted(DateTime.UtcNow);
                saga.MarkProviderBookingConfirmed(DateTime.UtcNow); saga.Complete(DateTime.UtcNow);
            }
        }
        else if (result.State == HotelInquiryState.Reserved && result.SupplyPriceMinor is > 0 && result.FeeMinor is >= 0 && result.FeeMinor <= result.SupplyPriceMinor
            && result.LockedUntilUtc > DateTime.UtcNow.AddSeconds(Policy.PaymentSafetySeconds)
            && (!reservation.ConfirmationAttempted || result.SafeToConfirmAgain))
        {
            if (request.Status is not (HotelRequestStatus.Created or HotelRequestStatus.ConvertedToOrder))
            { reservation.BlockPayment("LOCAL_STATE_REQUIRES_REVIEW"); return; }
            reservation.ReconcileReserved(result.SupplyPriceMinor.Value, result.FeeMinor.Value, result.LockedUntilUtc.Value, result.Snapshot, result.SafeToConfirmAgain);
            if (result.SupplyPriceMinor > request.TotalPrice) reservation.BlockPayment("SUPPLY_EXCEEDS_SALE");
            if (!request.OrderId.HasValue) request.SetPayableUntil(result.LockedUntilUtc.Value.AddSeconds(-Policy.PaymentSafetySeconds));
        }
        else if (result.State is HotelInquiryState.Rejected or HotelInquiryState.Cancelled && result.NoExternalLiability)
        {
            reservation.ReconcileNoLiability(result.State == HotelInquiryState.Cancelled, result.Snapshot);
            if (request.Status is not (HotelRequestStatus.Completed or HotelRequestStatus.ProviderConfirmed)) request.MarkFailed(DateTime.UtcNow);
        }
    }
}
