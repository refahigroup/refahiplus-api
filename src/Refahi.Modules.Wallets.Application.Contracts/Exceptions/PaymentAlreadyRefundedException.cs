using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class PaymentAlreadyRefundedException : WalletApplicationException
{
    public PaymentAlreadyRefundedException(Guid paymentId)
        : base("PAYMENT_ALREADY_REFUNDED", $"Payment {paymentId} has already been refunded.") { }
}
