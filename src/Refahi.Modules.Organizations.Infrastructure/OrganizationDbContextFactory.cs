using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Refahi.Modules.Organizations.Infrastructure;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{ public OrganizationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql("Host=localhost;Database=refahi_design;Username=design").Options); }
