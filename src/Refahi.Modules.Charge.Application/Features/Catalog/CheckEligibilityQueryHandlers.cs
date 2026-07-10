using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class CheckEligibilityQueryHandlers : IRequestHandler<CheckEligibilityQuery, ChargeEligibilityDto>
{
    private readonly IChargeProviderResolver _providers;

    public CheckEligibilityQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public Task<ChargeEligibilityDto> Handle(CheckEligibilityQuery q, CancellationToken ct)
    {
        return _providers.GetDefault()
                         .CheckEligibilityAsync(
                            new(
                                q.Operator, 
                                q.MobileNumber, 
                                q.AmountMinor, 
                                q.ProviderProductId, 
                                q.ProductCategory), 
                            ct
                         );
    }

}
