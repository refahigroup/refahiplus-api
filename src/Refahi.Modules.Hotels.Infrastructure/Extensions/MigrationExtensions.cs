using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Hotels.Infrastructure.Extensions;

public static class MigrationExtensions
{
    public static void ApplyPendingMigrations<TContext>(this IServiceProvider services)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = db.Database.GetPendingMigrations();
        if (pending.Any())
        {
            Console.WriteLine($"Applying EF migrations for {typeof(TContext).Name}...");
            db.Database.Migrate();
            Console.WriteLine("EF migrations applied successfully.");
        }
    }
}
