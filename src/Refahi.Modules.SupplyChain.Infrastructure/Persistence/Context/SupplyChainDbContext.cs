using Microsoft.EntityFrameworkCore;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;

public class SupplyChainDbContext : DbContext
{
    public SupplyChainDbContext(DbContextOptions<SupplyChainDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierLink> SupplierLinks => Set<SupplierLink>();
    public DbSet<SupplierAttachment> SupplierAttachments => Set<SupplierAttachment>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<AgreementProduct> AgreementProducts => Set<AgreementProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("supplychain");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupplyChainDbContext).Assembly);
    }
}
