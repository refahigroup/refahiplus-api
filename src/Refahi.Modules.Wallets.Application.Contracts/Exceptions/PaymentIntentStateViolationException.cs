using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class PaymentIntentStateViolationException : WalletApplicationException
{
    public PaymentIntentStateViolationException(string operation, string currentState)
        : base("PAYMENT_INTENT_STATE_VIOLATION", $"Cannot {operation} intent: already {currentState}.") { }
}
