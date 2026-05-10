using System;

namespace Refahi.Modules.PaymentGateway.Domain.Exceptions;

public class PaymentSessionAlreadyProcessedException : Exception
{
    public PaymentSessionAlreadyProcessedException(string message) : base(message) { }
}
