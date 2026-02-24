using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class RefundNotFoundException : WalletApplicationException
{
    public RefundNotFoundException(Guid refundId)
        : base("REFUND_NOT_FOUND", $"Refund {refundId} was not found.") { }
}
