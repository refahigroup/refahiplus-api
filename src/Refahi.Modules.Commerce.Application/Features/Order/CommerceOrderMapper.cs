using System.Text.Json;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Order;

internal static class CommerceOrderMapper
{
    internal static CommerceOrderDto Map(CommerceOrder order, bool reveal, ICommerceSecretProtector secrets, ICommerceProviderFactory providers)
    {
        var fulfillments = order.Fulfillments.Select(f =>
        {
            var delivery = f.DeliveryProtected == null ? null : JsonSerializer.Deserialize<CommerceFulfillmentResult>(secrets.Unprotect(f.DeliveryProtected));
            return new CommerceFulfillmentDto(f.Id, f.ProviderKey, f.Status.ToString(), f.ProviderOrderCode, f.FailureReason,
                f.Tickets.Select(t =>
                {
                    var code = secrets.Unprotect(t.CodeProtected);
                    var detail = delivery?.Tickets.FirstOrDefault(x => x.Code == code);
                    return new CommerceTicketDto(t.Id, t.IsChild, reveal ? code : null)
                    { Title = detail?.Title, QrCode = reveal ? detail?.QrCode : null };
                }).ToArray())
            { ProviderInvoiceId = f.ProviderInvoiceId, Documents = reveal ? delivery?.Documents ?? [] : [] };
        }).ToArray();
        var canCancel = order.Status is CommerceOrderStatus.PendingPayment or CommerceOrderStatus.FulfillmentPending or CommerceOrderStatus.Completed
            && order.Fulfillments.All(f => f.Status is ProviderFulfillmentStatus.Pending or ProviderFulfillmentStatus.Failed
                || f.Status == ProviderFulfillmentStatus.Completed && providers.GetRequired(f.ProviderKey).Capabilities.SupportsCancellation);
        return new(order.Id, order.OrderId, order.Status.ToString(), order.TotalAmountMinor,
            order.Items.Select(i => new CommerceOrderItemDto(i.Id, i.ProviderKey, i.SellerKey, i.Title, i.OfferTitle, i.PurchaseOptionKey,
                i.Quantity, i.UnitPriceMinor, i.CategoryCode)).ToArray(), fulfillments)
        { CanCancel = canCancel, PayableUntil = order.PayableUntil };
    }
}
