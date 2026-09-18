using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Features.Suppliers.GetSupplierProvisioningEligibility;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Xunit;

namespace Refahi.Modules.SupplyChain.Tests;

public sealed class SupplierProvisioningEligibilityTests
{
    [Fact]
    public async Task Contract_exposes_only_approved_eligibility_and_display_snapshot()
    {
        var supplier=Supplier.Create(SupplierType.Legal,null,null,"شرکت نمونه","برند نمونه",null,null,null,null,null,null,null,null,null,null,null,null);supplier.SubmitForReview();supplier.Approve();
        var result=await new GetSupplierProvisioningEligibilityQueryHandler(new Repository(supplier)).Handle(new GetSupplierProvisioningEligibilityQuery(supplier.Id),default);
        Assert.NotNull(result);Assert.True(result.IsApproved);Assert.False(result.IsTerminalLifecycleState);Assert.Equal("برند نمونه",result.DisplayName);
    }
    [Fact]
    public async Task Missing_supplier_returns_null()=>Assert.Null(await new GetSupplierProvisioningEligibilityQueryHandler(new Repository(null)).Handle(new GetSupplierProvisioningEligibilityQuery(Guid.NewGuid()),default));

    private sealed class Repository(Supplier? supplier):ISupplierRepository
    {
        public Task<Supplier?> GetByIdAsync(Guid id,bool includeChildren,CancellationToken ct)=>Task.FromResult(supplier?.Id==id?supplier:null);
        public Task<(IReadOnlyList<Supplier> Items,int Total)> GetPagedAsync(SupplierStatus? status,SupplierType? type,int? provinceId,string? search,int page,int size,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> ExistsByNationalIdAsync(string nationalId,Guid? excludeId,CancellationToken ct)=>throw new NotSupportedException();
        public Task AddAsync(Supplier value,CancellationToken ct)=>throw new NotSupportedException();public void Update(Supplier value)=>throw new NotSupportedException();public Task SaveChangesAsync(CancellationToken ct)=>throw new NotSupportedException();
    }
}
