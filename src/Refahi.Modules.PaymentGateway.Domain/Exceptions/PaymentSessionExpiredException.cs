using System;

namespace Refahi.Modules.PaymentGateway.Domain.Exceptions;

public class PaymentSessionExpiredException : Exception
{
    public PaymentSessionExpiredException(string message) : base(message) { }
}
