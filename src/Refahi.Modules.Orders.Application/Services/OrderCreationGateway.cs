using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ValidateHotelRequestForOrder;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Services;

public sealed class OrderCreationGateway : IOrderCreationGateway
{
    private const string HotelSourceModule = "Hotel";
    private const string HotelRequestReferenceType = "HotelRequest";

    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<OrderCreationGateway> _logger;

    public OrderCreationGateway(
        IOrderRepository orderRepository,
        IMediator mediator,
        ILogger<OrderCreationGateway> logger)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CreateOrderResponse> CreateAsync(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _orderRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            EnsureExistingOrderMatchesRequest(existing, request);

            using var existingScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["UserId"] = existing.UserId,
                ["SagaId"] = existing.SagaId,
                ["HotelRequestId"] = IsHotelRequestOrder(request) ? (Guid?)existing.SourceReferenceId : null,
                ["OrderId"] = existing.Id,
                ["ProviderBookingCode"] = null
            });

            _logger.LogInformation(
                "Order creation idempotency replayed. OrderId={OrderId}, SourceModule={SourceModule}, ReferenceType={ReferenceType}, SourceReferenceId={SourceReferenceId}, SagaId={SagaId}",
                existing.Id,
                existing.SourceModule,
                existing.ReferenceType,
                existing.SourceReferenceId,
                existing.SagaId);

            return new CreateOrderResponse(existing.Id, existing.OrderNumber, existing.FinalAmountMinor, existing.Currency);
        }

        if (IsHotelRequestOrder(request))
            await ValidateHotelRequestAsync(request, cancellationToken);

        var order = Order.Create(
            userId: request.UserId,
            sourceModule: request.SourceModule,
            sourceReferenceId: request.SourceReferenceId,
            idempotencyKey: request.IdempotencyKey,
            referenceType: request.ReferenceType,
            items: MapItems(request),
            shippingAddressId: request.ShippingAddressId,
            shippingAddressSnapshotJson: request.ShippingAddressSnapshotJson,
            deliveryDate: request.DeliveryDate,
            deliveryTimeSlot: (DeliveryTimeSlot)request.DeliveryTimeSlot,
            shippingFeeMinor: request.ShippingFeeMinor,
            discountCode: request.DiscountCode,
            discountCodeAmountMinor: request.DiscountCodeAmountMinor,
            sagaId: request.SagaId,
            payableUntil: request.PayableUntil);

        await _orderRepository.AddAsync(order, cancellationToken);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = order.UserId,
            ["SagaId"] = order.SagaId,
            ["HotelRequestId"] = IsHotelRequestOrder(request) ? (Guid?)order.SourceReferenceId : null,
            ["OrderId"] = order.Id,
            ["ProviderBookingCode"] = null
        });

        _logger.LogInformation(
            "Order created. OrderId={OrderId}, SourceModule={SourceModule}, ReferenceType={ReferenceType}, SourceReferenceId={SourceReferenceId}, SagaId={SagaId}",
            order.Id,
            order.SourceModule,
            order.ReferenceType,
            order.SourceReferenceId,
            order.SagaId);

        return new CreateOrderResponse(order.Id, order.OrderNumber, order.FinalAmountMinor, order.Currency);
    }

    private static List<OrderItemData> MapItems(CreateOrderCommand request)
        => request.Items.Select(i => new OrderItemData(
            Title: i.Title,
            UnitPriceMinor: i.UnitPriceMinor,
            Quantity: i.Quantity,
            DiscountAmountMinor: i.DiscountAmountMinor,
            SourceItemId: i.SourceItemId,
            CategoryCode: i.CategoryCode,
            Tags: i.Tags,
            MetadataJson: i.MetadataJson,
            DeliveryMethod: (DeliveryMethod)i.DeliveryMethod)).ToList();

    private async Task ValidateHotelRequestAsync(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.SourceModule.Equals(HotelSourceModule, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.ReferenceType, HotelRequestReferenceType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("سفارش هتل فقط با ReferenceType=HotelRequest قابل ایجاد است");
        }

        var hotelRequest = await _mediator.Send(
            new ValidateHotelRequestForOrderCommand(request.SourceReferenceId, request.UserId),
            cancellationToken);

        if (hotelRequest.TotalPrice != request.Items.Sum(i => (i.UnitPriceMinor * i.Quantity) - i.DiscountAmountMinor))
            throw new InvalidOperationException("مبلغ سفارش با درخواست هتل مطابقت ندارد");
    }

    private static bool IsHotelRequestOrder(CreateOrderCommand request)
        => request.SourceModule.Equals(HotelSourceModule, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(request.ReferenceType, HotelRequestReferenceType, StringComparison.OrdinalIgnoreCase);

    private static void EnsureExistingOrderMatchesRequest(Order existing, CreateOrderCommand request)
    {
        var referenceType = string.IsNullOrWhiteSpace(request.ReferenceType)
            ? request.SourceModule
            : request.ReferenceType.Trim();

        if (existing.UserId != request.UserId ||
            !existing.SourceModule.Equals(request.SourceModule, StringComparison.OrdinalIgnoreCase) ||
            existing.SourceReferenceId != request.SourceReferenceId ||
            !existing.ReferenceType.Equals(referenceType, StringComparison.OrdinalIgnoreCase) ||
            existing.SagaId != request.SagaId)
        {
            throw new InvalidOperationException("کلید یکتایی سفارش با درخواست دیگری ثبت شده است");
        }
    }
}
