using System;

namespace Refahi.Modules.PaymentGateway.Domain.Exceptions;

public class InvalidPaymentAmountException : Exception
{
    public InvalidPaymentAmountException(string message) : base(message) { }
}
