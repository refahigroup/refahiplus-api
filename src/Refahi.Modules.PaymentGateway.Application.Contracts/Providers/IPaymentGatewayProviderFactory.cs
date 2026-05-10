using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

/// <summary>
/// Factory for resolving payment gateway providers by type.
/// </summary>
public interface IPaymentGatewayProviderFactory
{
    IPaymentGatewayProvider GetProvider(PaymentGatewayProviderType providerType);
}
