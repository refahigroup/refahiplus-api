using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence
{
    /// <summary>
    /// Design-time factory برای ساخت HotelsDbContext در زمان Migration
    /// این کلاس فقط در زمان ابزار EF Core استفاده می‌شود (در Runtime کاری با آن نداریم).
    /// </summary>
    public sealed class HotelsDbContextFactory : IDesignTimeDbContextFactory<HotelsDbContext>
    {
        public HotelsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<HotelsDbContext>();
            optionsBuilder.UseNpgsql("");

            return new HotelsDbContext(optionsBuilder.Options);
        }
    }
}
