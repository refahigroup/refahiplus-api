using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers;

public class PaymentGatewayProviderFactory : IPaymentGatewayProviderFactory
{
    private readonly IEnumerable<IPaymentGatewayProvider> _providers;

    public PaymentGatewayProviderFactory(IEnumerable<IPaymentGatewayProvider> providers)
    {
        _providers = providers;
    }

    public IPaymentGatewayProvider GetProvider(PaymentGatewayProviderType providerType)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType)
            ?? throw new InvalidOperationException(
                $"درگاه پرداخت '{providerType}' پشتیبانی نمی‌شود یا ثبت نشده است.");

        return provider;
    }
}
