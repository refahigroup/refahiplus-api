using MediatR;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;
namespace Refahi.Modules.Entertainments.Application.Features.Checkout;

public sealed class EntertainmentOrderSourceValidator(IEntertainmentRepository repository) : INotificationHandler<ValidateOrderSourceNotification>
{
    public async Task Handle(ValidateOrderSourceNotification notification, CancellationToken ct)
    {
        var input = notification.Order;
        if (!input.SourceModule.Equals("Entertainments", StringComparison.OrdinalIgnoreCase)) return;
        if (input.ReferenceType != "EntertainmentPurchase" || input.SourceReferenceId is not Guid id)
            throw Invalid();
        var purchase = await repository.GetOrderAsync(id, ct);
        if (purchase is null || purchase.UserId != input.UserId || purchase.Status != EntertainmentPurchaseStatus.AwaitingPayment ||
            purchase.OrderId.HasValue || purchase.PayableUntil is null || purchase.PayableUntil <= DateTimeOffset.UtcNow || input.PayableUntil != purchase.PayableUntil ||
            input.IdempotencyKey != $"entertainments-order:{id:N}" || input.Items.Count != purchase.Items.Count ||
            input.Items.Select(x => x.SourceItemId).Distinct().Count() != input.Items.Count || input.ShippingFeeMinor != 0 ||
            input.DiscountCodeAmountMinor != 0 || input.DiscountCode is not null || input.FinancialSnapshot is not null || input.PaymentPostings?.Count > 0 ||
            input.Items.Any(x => !purchase.Items.Any(y => y.Id == x.SourceItemId && y.CategoryCode == x.CategoryCode &&
                y.Quantity == x.Quantity && y.UnitPriceMinor == x.UnitPriceMinor && x.DiscountAmountMinor == 0))) throw Invalid();
    }
    private static EntertainmentDomainException Invalid() => new("سفارش مالی با رزرو خدمات تفریحی مطابقت ندارد", "ORDER_SOURCE_MISMATCH");
}
