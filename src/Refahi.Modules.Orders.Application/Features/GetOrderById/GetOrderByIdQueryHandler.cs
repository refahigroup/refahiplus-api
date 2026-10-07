using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Orders.Domain.Aggregates;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetOrderById;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEnumerable<IOrderPaymentParticipant> _participants;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository, IEnumerable<IOrderPaymentParticipant>? participants = null)
    {
        _orderRepository = orderRepository;
        _participants = participants ?? [];
    }

    public async Task<OrderDto?> Handle(
        GetOrderByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(
            request.OrderId,
            cancellationToken
        );
        if (order is null)
            return null;

        // Ownership check: User role can only see their own orders
        // Return null (404) instead of Forbidden to prevent GUID enumeration
        if (request.CallerRole == "User" && order.UserId != request.CallerUserId)
            return null;

        var items = order
            .Items.Select(i => new OrderItemDto(
                Id: i.Id,
                Title: i.Title,
                UnitPriceMinor: i.UnitPriceMinor,
                Quantity: i.Quantity,
                FinalPriceMinor: i.FinalPriceMinor,
                SourceItemId: i.SourceItemId,
                CategoryCode: i.CategoryCode,
                Tags: i.Tags,
                MetadataJson: i.MetadataJson,
                DeliveryMethod: (short)i.DeliveryMethod
            ))
            .ToList();

        var paymentEligibility = order.GetPaymentEligibility(DateTimeOffset.UtcNow);
        if (paymentEligibility.CanPay)
        {
            var source = _participants.SingleOrDefault(p => p.SourceModule.Equals(order.SourceModule, StringComparison.OrdinalIgnoreCase));
            var reason = source is null ? null : await source.GetUnavailableReasonAsync(new(order.Id, order.UserId, order.SourceReferenceId, order.FinalAmountMinor), cancellationToken);
            if (reason is not null) paymentEligibility = OrderPaymentEligibility.Unavailable(reason);
        }

        return new OrderDto(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            TotalAmountMinor: order.TotalAmountMinor,
            DiscountAmountMinor: order.DiscountAmountMinor,
            ShippingFeeMinor: order.ShippingFeeMinor,
            DiscountCode: order.DiscountCode,
            DiscountCodeAmountMinor: order.DiscountCodeAmountMinor,
            FinalAmountMinor: order.FinalAmountMinor,
            Status: order.Status.ToString(),
            PaymentState: order.PaymentState.ToString(),
            SourceModule: order.SourceModule,
            SourceReferenceId: order.SourceReferenceId,
            ReferenceType: order.ReferenceType,
            ShippingAddressId: order.ShippingAddressId,
            ShippingAddressSnapshotJson: order.ShippingAddressSnapshotJson,
            DeliveryDate: order.DeliveryDate,
            DeliveryTimeSlot: (short)order.DeliveryTimeSlot,
            Items: items,
            CreatedAt: order.CreatedAt,
            SourceOwnerId: order.SourceOwnerId,
            SourceShopId: order.SourceShopId,
            CreatedByUserId: order.CreatedByUserId,
            GrossAmountMinor: order.GrossAmountMinor,
            CommissionPercent: order.CommissionPercent,
            CommissionAmountMinor: order.CommissionAmountMinor,
            VatPercent: order.VatPercent,
            VatAmountMinor: order.VatAmountMinor,
            RecipientNetAmountMinor: order.RecipientNetAmountMinor,
            CanPay: paymentEligibility.CanPay,
            PaymentUnavailableReason: paymentEligibility.UnavailableReason,
            PayableUntil: order.PayableUntil
        );
    }
}
