using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;

public class PaymentSessionNotFoundException : Exception
{
    public PaymentSessionNotFoundException(Guid sessionId)
        : base($"جلسه پرداخت با شناسه {sessionId} یافت نشد.") { }
}
