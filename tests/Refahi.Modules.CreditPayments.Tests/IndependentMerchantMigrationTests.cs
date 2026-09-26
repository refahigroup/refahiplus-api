using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Application.Features.CreateWallet;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class IndependentMerchantMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Fresh_migrations_create_independent_merchant_and_idempotent_owned_wallet()
    {
        var connection = postgres.GetConnectionString();
        await using (var wallets = new WalletsDbContext(new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(connection).Options))
            await wallets.Database.MigrateAsync();
        await using (var credit = new CreditPaymentsDbContext(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(connection).Options))
            await credit.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow;
        var merchant = Merchant.Create(MerchantEntityType.Legal, "12345678901", "شرکت نمونه", null, now);
        var walletHandler = new CreateWalletCommandHandler(new WalletReadRepository(connection), new WalletWriteRepository(connection));
        var first = await walletHandler.Handle(new CreateWalletCommand(merchant.Id, WalletTypeCodes.Merchant, "IRR"), default);
        var repeated = await walletHandler.Handle(new CreateWalletCommand(merchant.Id, WalletTypeCodes.Merchant, "IRR"), default);
        Assert.Equal(first.WalletId, repeated.WalletId);
        merchant.BindSettlementWallet(first.WalletId, now);

        await using (var credit = new CreditPaymentsDbContext(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(connection).Options))
        {
            credit.Merchants.Add(merchant);
            await credit.SaveChangesAsync();
            credit.Merchants.Add(Merchant.Create(MerchantEntityType.Legal, merchant.NationalId, "تکراری", null, now));
            await Assert.ThrowsAsync<DbUpdateException>(() => credit.SaveChangesAsync());
        }

        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='credit_payments' AND table_name='merchants' AND column_name='supplier_id')", sql);
        Assert.False((bool)(await command.ExecuteScalarAsync())!);

        var payer = await new WalletWriteRepository(connection).CreateAsync(Guid.NewGuid(), 2, 1, "IRR");
        await new WalletAtomicWriter(connection).ExecuteTopUpAsync(payer, "merchant-test-topup", 1_000, "IRR", null, null, default);
        var payments = new PaymentAtomicWriter(connection);
        var intent = await payments.ExecuteCreateIntentAsync(Guid.NewGuid(), "merchant-test-payment", 600,
            "IRR", [new AllocationInput(payer, 600)], null, first.WalletId, default);
        var capture = await payments.ExecuteCaptureIntentAsync(intent.IntentId, "merchant-test-capture", default);
        await using (var balance = new NpgsqlCommand("SELECT available_minor FROM wallets.wallet_balances WHERE wallet_id = @wallet", sql))
        {
            balance.Parameters.AddWithValue("wallet", first.WalletId);
            Assert.Equal(600L, (long)(await balance.ExecuteScalarAsync())!);
        }
        await payments.ExecuteRefundPaymentAsync(capture.PaymentId, "merchant-test-refund", null, null, default);
        await using (var balance = new NpgsqlCommand("SELECT available_minor FROM wallets.wallet_balances WHERE wallet_id = @wallet", sql))
        {
            balance.Parameters.AddWithValue("wallet", first.WalletId);
            Assert.Equal(0L, (long)(await balance.ExecuteScalarAsync())!);
        }
    }
}
