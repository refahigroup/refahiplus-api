using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Wallets.Application.Contracts.Infrastructure;
using Wallets.Application.Contracts.Repositories;
using Wallets.Infrastructure.Persistence.Atomic;
using Wallets.Infrastructure.Persistence.Context;
using Wallets.Infrastructure.Persistence.Repositories;

namespace Wallets.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Default") ?? configuration["ConnectionStrings:Default"];
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Connection string 'Default' is required (ConnectionStrings:Default).");

        services.AddDbContext<WalletsDbContext>(options =>
            options.UseNpgsql(conn, b => b.MigrationsAssembly(typeof(WalletsDbContext).Assembly.FullName)));

        // Read repositories (Dapper-based queries)
        services.AddScoped<IWalletReadRepository>(sp => new WalletReadRepository(conn));

        // Atomic writers (explicit SQL transaction execution)
        services.AddScoped<IWalletAtomicWriter>(sp => new WalletAtomicWriter(conn));

        return services;
    }
}