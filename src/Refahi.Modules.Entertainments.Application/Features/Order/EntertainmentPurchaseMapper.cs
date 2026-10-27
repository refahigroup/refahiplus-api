using System.Text.Json;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Order;

internal static class EntertainmentPurchaseMapper
{
    internal static EntertainmentPurchaseDto Map(EntertainmentPurchase order, bool reveal, IEntertainmentSecretProtector secrets, IEntertainmentProviderFactory providers)
    {
        var fulfillments = order.Fulfillments.Select(f =>
        {
            var delivery = f.DeliveryProtected == null ? null : JsonSerializer.Deserialize<EntertainmentFulfillmentResult>(secrets.Unprotect(f.DeliveryProtected));
            return new EntertainmentFulfillmentDto(f.Id, f.ProviderKey, f.Status.ToString(), f.ProviderOrderCode, f.FailureReason,
                f.Tickets.Select(t =>
                {
                    var code = secrets.Unprotect(t.CodeProtected);
                    var detail = delivery?.Tickets.FirstOrDefault(x => x.Code == code);
                    return new EntertainmentTicketDto(t.Id, t.IsChild, reveal ? code : null)
                    { Title = detail?.Title, QrCode = reveal ? detail?.QrCode : null };
                }).ToArray())
            { ProviderInvoiceId = f.ProviderInvoiceId, Documents = reveal ? delivery?.Documents ?? [] : [] };
        }).ToArray();
        var canCancel = order.Status is EntertainmentPurchaseStatus.AwaitingPayment or EntertainmentPurchaseStatus.IssuancePending or EntertainmentPurchaseStatus.Completed
            && order.Fulfillments.All(f => f.Status is ProviderFulfillmentStatus.Pending or ProviderFulfillmentStatus.Failed
                || f.Status == ProviderFulfillmentStatus.Completed && providers.GetEnabledProviders().FirstOrDefault(x => x.Key.Equals(f.ProviderKey, StringComparison.OrdinalIgnoreCase))?.Capabilities.SupportsCancellation == true);
        return new(order.Id, order.OrderId, order.Status.ToString(), order.TotalAmountMinor,
            order.Items.Select(i => new EntertainmentPurchaseItemDto(i.Id, i.ProviderKey, i.SupplierKey, i.Title, i.OfferTitle, i.PurchaseOptionKey,
                i.Quantity, i.UnitPriceMinor, i.CategoryCode)).ToArray(), fulfillments)
        { CanCancel = canCancel, PayableUntil = order.PayableUntil };
    }
}
