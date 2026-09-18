using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.GetSupplierProvisioningEligibility;

public sealed class GetSupplierProvisioningEligibilityQueryHandler(ISupplierRepository repository):IRequestHandler<GetSupplierProvisioningEligibilityQuery,SupplierProvisioningEligibilityDto?>
{
    public async Task<SupplierProvisioningEligibilityDto?> Handle(GetSupplierProvisioningEligibilityQuery request,CancellationToken ct)
    {
        var supplier=await repository.GetByIdAsync(request.SupplierId,false,ct);
        if(supplier is null||supplier.IsDeleted)return null;
        var display=supplier.BrandName??supplier.CompanyName??string.Join(' ',new[]{supplier.FirstName,supplier.LastName}.Where(x=>!string.IsNullOrWhiteSpace(x)));
        return new(supplier.Id,display,(short)supplier.Status,supplier.Status==SupplierStatus.Approved,supplier.Status==SupplierStatus.Rejected);
    }
}
