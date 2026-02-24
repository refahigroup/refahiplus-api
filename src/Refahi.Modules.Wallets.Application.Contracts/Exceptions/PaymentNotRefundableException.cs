using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class PaymentNotRefundableException : WalletApplicationException
{
    public PaymentNotRefundableException(Guid paymentId, string reason)
        : base("PAYMENT_NOT_REFUNDABLE", $"Payment {paymentId} cannot be refunded: {reason}.") { }
}
