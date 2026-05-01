using Microsoft.EntityFrameworkCore;
using Refahi.Shared.Infrastructure;

namespace Refahi.Api.Services;

public class DbTools : IDbTools
{
    private readonly IServiceProvider _serviceProvider;

    public DbTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void ApplyMigrations<T>()
    {
        T service = _serviceProvider.GetRequiredService<T>();

        if (service is null)
            throw new Exception("Exception in ApplyMigrations: Invalid service");

        DbContext context = service as DbContext;

        if (context is null)
            throw new Exception("Exception in ApplyMigrations: Invalid DbContext");

        try
        {
            var pending = context.Database.GetPendingMigrations();

            if (pending.Any())
            {
                Console.WriteLine($"Applying EF migrations for {typeof(T).Name}...");
                context.Database.Migrate();
                Console.WriteLine($"EF migrations for {typeof(T).Name} applied successfully.");
            }
        }
        catch (Exception ex)
        {
            var e = new Exception($"Exception in ApplyMigrations: {ex.Message}", ex);
            Console.WriteLine(e.Message, e);
            throw e;
        }
    }
}

public static class DbToolsRegisterar
{
    public static IServiceCollection RegisterDbTools(this IServiceCollection services)
    {
        services.AddScoped<IDbTools, DbTools>();

        return services;
    }
}
