using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase02SupplierValidationTests
{
    private static readonly Guid Approved=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Unapproved=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    [Fact] public async Task Approved_supplier_can_be_provisioned(){var (mediator,repo)=Build();var result=await mediator.Send(new CreateMerchantCommand(Approved,null));Assert.Equal(Approved,result.SupplierId);Assert.Equal("برند تاییدشده",result.DisplayName);Assert.Single(repo.Merchants);}
    [Fact] public async Task Missing_supplier_is_rejected(){var (mediator,_)=Build();await Assert.ThrowsAsync<CreditPaymentsDomainException>(()=>mediator.Send(new CreateMerchantCommand(Guid.NewGuid(),"x")));}
    [Fact] public async Task Unapproved_supplier_is_rejected(){var (mediator,_)=Build();await Assert.ThrowsAsync<CreditPaymentsDomainException>(()=>mediator.Send(new CreateMerchantCommand(Unapproved,"x")));}
    [Fact] public async Task Duplicate_supplier_merchant_is_rejected(){var (mediator,_)=Build();await mediator.Send(new CreateMerchantCommand(Approved,"x"));await Assert.ThrowsAsync<CreditPaymentsDomainException>(()=>mediator.Send(new CreateMerchantCommand(Approved,"x")));}
    private static (IMediator Mediator,MemoryRepository Repository) Build(){var repo=new MemoryRepository();var services=new ServiceCollection();services.AddSingleton<ICreditPaymentsRepository>(repo);services.AddMediatR(typeof(DI).Assembly,typeof(Phase02SupplierValidationTests).Assembly);return(services.BuildServiceProvider().GetRequiredService<IMediator>(),repo);}

    public sealed class SupplierHandler:IRequestHandler<GetSupplierProvisioningEligibilityQuery,SupplierProvisioningEligibilityDto?>
    {
        public Task<SupplierProvisioningEligibilityDto?> Handle(GetSupplierProvisioningEligibilityQuery r,CancellationToken ct)=>Task.FromResult(r.SupplierId==Approved?new SupplierProvisioningEligibilityDto(Approved,"برند تاییدشده",3,true,false):r.SupplierId==Unapproved?new SupplierProvisioningEligibilityDto(Unapproved,"در انتظار",2,false,false):null);
    }
    private sealed class MemoryRepository:ICreditPaymentsRepository
    {
        public List<Merchant> Merchants {get;}=[];
        public Task<Merchant?> GetMerchantAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult(Merchants.SingleOrDefault(x=>x.Id==id));
        public Task<Merchant?> GetMerchantBySupplierAsync(Guid id,CancellationToken ct)=>Task.FromResult(Merchants.SingleOrDefault(x=>x.SupplierId==id));
        public Task<Terminal?> GetTerminalAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult<Terminal?>(null);
        public Task<TerminalCredential?> GetCredentialAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult<TerminalCredential?>(null);
        public Task<(TerminalCredential Credential,Terminal Terminal,Merchant Merchant)?> GetAuthenticationGraphAsync(string id,CancellationToken ct)=>Task.FromResult<(TerminalCredential,Terminal,Merchant)?>(null);
        public void Add(Merchant x)=>Merchants.Add(x);public void AddAudit(AuditRecord x){}public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    }
}
