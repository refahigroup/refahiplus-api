using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;

public sealed class FundingIdempotencyConflictException : Exception
{
    public FundingIdempotencyConflictException()
        : base("کلید تکرار با درخواست متفاوتی استفاده شده است.") { }
}
