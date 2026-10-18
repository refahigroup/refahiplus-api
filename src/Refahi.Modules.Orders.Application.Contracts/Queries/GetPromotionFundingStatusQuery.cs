using MediatR;
namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetPromotionFundingStatusQuery : IRequest<PromotionFundingStatusDto>;
public sealed record PromotionFundingStatusDto(bool Enabled, Guid? WalletId, bool IsValid, long AvailableMinor, long HeldMinor);
