using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class PaymentIntentNotFoundException : WalletApplicationException
{
    public PaymentIntentNotFoundException(Guid intentId)
        : base("PAYMENT_INTENT_NOT_FOUND", $"Payment intent {intentId} was not found.") { }
}
