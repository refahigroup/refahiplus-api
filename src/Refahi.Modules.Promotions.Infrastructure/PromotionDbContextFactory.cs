using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Refahi.Modules.Promotions.Infrastructure;

public sealed class PromotionDbContextFactory : IDesignTimeDbContextFactory<PromotionDbContext>
{ public PromotionDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PromotionDbContext>().UseNpgsql("Host=localhost;Database=refahi_design;Username=design").Options); }
