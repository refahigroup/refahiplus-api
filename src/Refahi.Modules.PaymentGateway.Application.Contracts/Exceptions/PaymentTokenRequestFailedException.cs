using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;

public class PaymentTokenRequestFailedException : Exception
{
    public PaymentTokenRequestFailedException(string message) : base(message) { }
}
