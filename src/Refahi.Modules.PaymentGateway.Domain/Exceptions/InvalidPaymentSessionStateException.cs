using System;

namespace Refahi.Modules.PaymentGateway.Domain.Exceptions;

public class InvalidPaymentSessionStateException : Exception
{
    public InvalidPaymentSessionStateException(string message) : base(message) { }
}
