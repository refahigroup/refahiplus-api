using MediatR;
namespace Refahi.Modules.Orders.Application.Contracts.Commands;

public sealed record OrderPromotionItemDto(Guid OrderItemId, long AmountMinor);
public sealed record AppliedOrderPromotionDto(Guid OrderId, long PricingVersion, long AmountBeforePromotionMinor,
    long DiscountCodeAmountMinor, long FinalAmountMinor, Guid? ReservationId, DateTimeOffset? ExpiresAt,
    IReadOnlyList<OrderPromotionItemDto> Items);
public sealed record PreviewOrderPromotionQuery(Guid OrderId, Guid UserId, string Code, long ExpectedPricingVersion) : IRequest<AppliedOrderPromotionDto>;
public sealed record ApplyOrderPromotionCommand(Guid OrderId, Guid UserId, string Code, long ExpectedPricingVersion, string IdempotencyKey) : IRequest<AppliedOrderPromotionDto>;
public sealed record RemoveOrderPromotionCommand(Guid OrderId, Guid UserId, long ExpectedPricingVersion, string IdempotencyKey) : IRequest<AppliedOrderPromotionDto>;
public sealed record RecoverOrderPromotionCommand(Guid OrderId, Guid? ReservationId = null) : IRequest;
