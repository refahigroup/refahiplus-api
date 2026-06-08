using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetOrderPaymentOptionsQuery(
    Guid OrderId,
    Guid CallerUserId,
    string CallerRole
) : IRequest<OrderPaymentOptionsDto?>;

public sealed record OrderPaymentOptionsDto(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Currency,
    string PaymentState,
    List<OrderWalletOptionDto> Wallets,
    List<OrderWalletAllocationSuggestionDto> Allocations,
    long TotalSuggestedMinor,
    bool IsCovered,
    long DeficitMinor);

public sealed record OrderWalletOptionDto(
    Guid WalletId,
    string WalletType,
    long AvailableBalanceMinor,
    long TotalBalanceMinor,
    long HeldAmountMinor,
    string Currency,
    string? AllowedCategoryCode,
    DateTimeOffset? ContractExpiresAt,
    bool IsAllowed,
    string? NotAllowedReason);

public sealed record OrderWalletAllocationSuggestionDto(
    Guid WalletId,
    string WalletType,
    long AmountMinor,
    long AvailableBalanceMinor);
