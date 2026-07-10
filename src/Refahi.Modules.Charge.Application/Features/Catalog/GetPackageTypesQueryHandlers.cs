using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetPackageTypesQueryHandlers : IRequestHandler<GetPackageTypesQuery, IReadOnlyList<PackageTypeDto>>
{
    private readonly IChargeProviderResolver _providers;

    public GetPackageTypesQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public Task<IReadOnlyList<PackageTypeDto>> Handle(GetPackageTypesQuery q, CancellationToken ct)
    {
        return _providers.GetDefault().GetPackageTypesAsync(ct);
    }
}
