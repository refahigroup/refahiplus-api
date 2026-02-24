using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Application.Contracts.Interfaces;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;
using System;

namespace Refahi.Modules.Wallets.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionStringName = configuration.GetSection("ConnectionString").Value;
        var connectionString = configuration.GetConnectionString(connectionStringName);

        //var conn = configuration.GetConnectionString("Default") ?? configuration["ConnectionStrings:Default"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string is required.");

        services.AddDbContext<WalletsDbContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(WalletsDbContext).Assembly.FullName)));

        // Read repositories (Dapper-based queries)
        services.AddScoped<IWalletReadRepository>(sp => new WalletReadRepository(connectionString));
        services.AddScoped<IPaymentReadRepository>(sp => new PaymentReadRepository(connectionString));

        // Atomic writers (explicit SQL transaction execution)
        services.AddScoped<IWalletAtomicWriter>(sp => new WalletAtomicWriter(connectionString));
        services.AddScoped<IPaymentAtomicWriter>(sp => new PaymentAtomicWriter(connectionString));
        
        // Balance rebuilder (reconciliation & drift repair)
        services.AddScoped<IBalanceRebuilder>(sp => 
            new BalanceRebuilder(connectionString, sp.GetRequiredService<ILogger<BalanceRebuilder>>()));

        return services;
    }
}