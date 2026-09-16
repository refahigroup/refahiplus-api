using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;

public sealed class ProviderTransactionAlreadyClaimedException : Exception
{
    public ProviderTransactionAlreadyClaimedException()
        : base("این تراکنش درگاه قبلاً به جلسه دیگری متصل شده است.") { }
}
