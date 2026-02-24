using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Application.Contracts.Interfaces;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using System;
using Wallets.Infrastructure.Persistence.Repositories;

namespace Refahi.Modules.Wallets.Infrastructure;

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
        services.AddScoped<IPaymentReadRepository>(sp => new PaymentReadRepository(conn));

        // Atomic writers (explicit SQL transaction execution)
        services.AddScoped<IWalletAtomicWriter>(sp => new WalletAtomicWriter(conn));
        services.AddScoped<IPaymentAtomicWriter>(sp => new PaymentAtomicWriter(conn));
        
        // Balance rebuilder (reconciliation & drift repair)
        services.AddScoped<IBalanceRebuilder>(sp => 
            new BalanceRebuilder(conn, sp.GetRequiredService<ILogger<BalanceRebuilder>>()));

        return services;
    }
}