using System.Text.Json;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Order;

internal static class EntertainmentPurchaseMapper
{
    private static string? Clean(string? value) => Refahi.Modules.Entertainments.Application.Contracts.Discovery.EntertainmentPresentation.Text(value);
    internal static EntertainmentPurchaseDto Map(EntertainmentPurchase order, bool reveal, IEntertainmentSecretProtector secrets, IEntertainmentProviderFactory providers)
    {
        var fulfillments = order.Fulfillments.Select(f =>
        {
            var delivery = f.DeliveryProtected == null ? null : JsonSerializer.Deserialize<EntertainmentFulfillmentResult>(secrets.Unprotect(f.DeliveryProtected));
            var tickets = f.Tickets.Select(t =>
            {
                var storedCode = secrets.Unprotect(t.CodeProtected);
                var detail = delivery?.Tickets.FirstOrDefault(x => x.Code == storedCode);
                var code = detail == null ? null : EntertainmentAdmission.Code(detail);
                var qr = detail == null ? null : EntertainmentAdmission.Credential(detail.QrCode);
                return new EntertainmentTicketDto(t.Id, t.IsChild, reveal ? code : null)
                {
                    Title = Clean(detail?.Title), QrCode = reveal ? qr : null, ProgramTitle = Clean(detail?.ProgramTitle),
                    VenueTitle = Clean(detail?.VenueTitle), Address = Clean(detail?.Address), StartsAt = detail?.StartsAt
                };
            }).ToArray();
            var ready = delivery?.Tickets.Count > 0 && delivery.Tickets.All(EntertainmentAdmission.Ready);
            return new EntertainmentFulfillmentDto(f.Id, f.Status.ToString(), f.Status == ProviderFulfillmentStatus.Completed ? (ready ? "Ready" : "NeedsReview") : "Pending", tickets);
        }).ToArray();
        var canCancel = order.Status is EntertainmentPurchaseStatus.AwaitingPayment or EntertainmentPurchaseStatus.IssuancePending or EntertainmentPurchaseStatus.Completed
            && order.Fulfillments.All(f => f.Status is ProviderFulfillmentStatus.Pending or ProviderFulfillmentStatus.Failed
                || f.Status == ProviderFulfillmentStatus.Completed && providers.GetEnabledProviders().FirstOrDefault(x => x.Key.Equals(f.ProviderKey, StringComparison.OrdinalIgnoreCase))?.Capabilities.SupportsCancellation == true);
        return new(order.Id, order.OrderId, order.Status.ToString(), order.TotalAmountMinor,
            order.Items.Select(i => new EntertainmentPurchaseItemDto(i.Id, Clean(i.Title)!, Clean(i.OfferTitle)!, i.PurchaseOptionKey,
                i.Quantity, i.UnitPriceMinor, i.CategoryCode)).ToArray(), fulfillments)
        { CanCancel = canCancel, PayableUntil = order.PayableUntil };
    }
}
