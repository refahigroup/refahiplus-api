using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

public sealed record PlaceStoreOrderCommand(
    Guid UserId,
    List<WalletPaymentInput> WalletAllocations
) : IRequest<PlaceStoreOrderResponse>;

public sealed record WalletPaymentInput(Guid WalletId, long AmountMinor);

public sealed record PlaceStoreOrderResponse(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Status);
