using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class PaymentNotFoundException : WalletApplicationException
{
    public PaymentNotFoundException(Guid paymentId)
        : base("PAYMENT_NOT_FOUND", $"Payment {paymentId} was not found.") { }
}
