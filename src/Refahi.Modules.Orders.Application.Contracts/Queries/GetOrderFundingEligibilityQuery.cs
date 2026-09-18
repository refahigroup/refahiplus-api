using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

/// <summary>
/// Trusted, allocation-free funding projection for another bounded context.
/// Allocation internals stay inside Orders. The permitted personal Wallet identifier
/// is returned only to trusted backend consumers so they can request a top-up without
/// allowing a browser or merchant to select a Wallet.
/// </summary>
public sealed record GetOrderFundingEligibilityQuery(Guid OrderId, Guid UserId)
    : IRequest<OrderFundingEligibilityDto?>;

public sealed record OrderFundingEligibilityDto(
    Guid OrderId,
    Guid UserId,
    long OrderAmountMinor,
    string Currency,
    long TotalEligibleSpendableAmountMinor,
    bool IsSufficient,
    long DeficitAmountMinor,
    bool HasPermittedPersonalWallet,
    DateTimeOffset EvaluatedAt,
    Guid? PermittedPersonalWalletId = null
);
