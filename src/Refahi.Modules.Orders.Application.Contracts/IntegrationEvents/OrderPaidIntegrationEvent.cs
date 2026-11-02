using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

public sealed record OrderPaidIntegrationEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid? SourceReferenceId,
    string ReferenceType,
    Guid? SagaId,
    Guid PaymentId,
    long AmountMinor,
    DateTimeOffset OccurredAt,
    long SourceAmountMinor = 0,
    long AmountBeforePromotionMinor = 0,
    long PromotionFundingMinor = 0
) : INotification;
