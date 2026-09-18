using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Refahi.Modules.CreditPayments.Api;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase01FoundationTests
{
    [Fact]
    public void All_module_assemblies_use_the_expected_namespace_family()
    {
        var types = new[]
        {
            typeof(Refahi.Modules.CreditPayments.Domain.AssemblyMarker),
            typeof(Refahi.Modules.CreditPayments.Application.Contracts.AssemblyMarker),
            typeof(Refahi.Modules.CreditPayments.Application.DI),
            typeof(Refahi.Modules.CreditPayments.Infrastructure.DI),
            typeof(DI),
        };

        Assert.All(types, type => Assert.StartsWith("Refahi.Modules.CreditPayments.", type.Namespace));
    }

    [Fact]
    public void Registration_adds_the_module_db_context_without_cross_module_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStringName"] = "Default",
                ["ConnectionStrings:Default"] = "Host=localhost;Database=refahi;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();

        services.RegisterCreditPaymentsModule(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(CreditPaymentsDbContext));
    }

    [Fact]
    public void DbContext_uses_the_dedicated_schema_and_approved_phase10_tables()
    {
        var options = new DbContextOptionsBuilder<CreditPaymentsDbContext>()
            .UseNpgsql("Host=localhost;Database=refahi;Username=test;Password=test")
            .Options;
        using var context = new CreditPaymentsDbContext(options);

        Assert.Equal("credit_payments", context.Model.GetDefaultSchema());
        var tables = context.Model.GetEntityTypes().Select(x => x.GetTableName()).ToHashSet();
        Assert.Contains("merchants", tables);
        Assert.Contains("terminals", tables);
        Assert.Contains("terminal_credentials", tables);
        Assert.Contains("gateway_payments", tables);
        Assert.Contains("idempotency_records", tables);
        Assert.Contains("checkout_sessions", tables);
        Assert.Contains("checkout_otp_challenges", tables);
        Assert.Contains("payment_attempts", tables);
        Assert.Contains("funding_continuations", tables);
        Assert.Contains("refunds", tables);
        Assert.Contains("webhook_events", tables);
        Assert.Contains("outbox_messages", tables);
        Assert.Contains("webhook_deliveries", tables);
    }

    [Fact]
    public void Api_prefix_remains_stable()
    {
        Assert.Equal("/api/credit-payments/v1", DI.ApiPrefix);
    }

    [Fact]
    public void Domain_isolated_from_application_infrastructure_and_api()
    {
        Assert.DoesNotContain(typeof(Refahi.Modules.CreditPayments.Domain.AssemblyMarker)
            .Assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "Refahi.Modules.CreditPayments.Application"
                or "Refahi.Modules.CreditPayments.Infrastructure"
                or "Refahi.Modules.CreditPayments.Api");
    }

    [Fact]
    public void Application_isolated_from_infrastructure_and_api()
    {
        Assert.DoesNotContain(typeof(Refahi.Modules.CreditPayments.Application.DI)
            .Assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "Refahi.Modules.CreditPayments.Infrastructure"
                or "Refahi.Modules.CreditPayments.Api");
    }

    [Fact]
    public void Application_has_only_the_approved_cross_module_contract_dependencies()
    {
        var references=typeof(Refahi.Modules.CreditPayments.Application.DI).Assembly.GetReferencedAssemblies().Select(x=>x.Name??string.Empty).ToArray();
        Assert.Contains("Refahi.Modules.SupplyChain.Application.Contracts",references);
        Assert.Contains("Refahi.Modules.Identity.Application.Contracts",references);
        Assert.Contains("Refahi.Modules.Orders.Application.Contracts",references);
        Assert.Contains("Refahi.Modules.PaymentGateway.Application.Contracts",references);
        Assert.Contains("Refahi.Modules.Wallets.Application.Contracts",references);
        Assert.DoesNotContain(references,x=>x.Contains("SupplyChain.Domain")||x.Contains("SupplyChain.Infrastructure")||x.Contains("Wallets.Domain")||x.Contains("Wallets.Infrastructure")||x.Contains("Orders.Domain")||x.Contains("Orders.Infrastructure")||x.Contains("PaymentGateway.Domain")||x.Contains("PaymentGateway.Infrastructure")||x.Contains("Identity.Domain")||x.Contains("Identity.Infrastructure")||x.Contains("Settlement"));
    }

    [Fact]
    public void Contracts_isolated_from_persistence_and_api()
    {
        Assert.DoesNotContain(typeof(Refahi.Modules.CreditPayments.Application.Contracts.AssemblyMarker)
            .Assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "Refahi.Modules.CreditPayments.Infrastructure"
                or "Refahi.Modules.CreditPayments.Api"
                or "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Infrastructure_has_no_premature_business_module_dependencies()
    {
        var forbidden = new[] { "Wallets", "Orders", "PaymentGateway", "Identity", "SupplyChain", "Settlement" };
        var references = typeof(Refahi.Modules.CreditPayments.Infrastructure.DI)
            .Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference => forbidden.Any(reference.Contains));
    }

    [Fact]
    public void Domain_has_no_cross_module_business_dependencies()
    {
        var references = typeof(Refahi.Modules.CreditPayments.Domain.AssemblyMarker)
            .Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference => reference.StartsWith("Refahi.Modules.")
            && !reference.StartsWith("Refahi.Modules.CreditPayments."));
    }
}
