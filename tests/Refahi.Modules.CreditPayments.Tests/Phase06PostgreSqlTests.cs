using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase06PostgreSqlTests : IAsyncLifetime
{
    private const string Phase05="20260916143327_CreditPayments_AddOrderBinding";
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private static readonly DateTimeOffset Now=new(2026,9,16,20,0,0,TimeSpan.Zero);
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();

    [Fact(Skip = "Legacy staged migration uses the retired Supplier merchant model; current migration is covered separately.")]
    public async Task Phase05_to_phase06_creates_constraints_preserves_history_and_rolls_back()
    {
        await ResetTo(Phase05);
        await using var db=Create();
        var ids=await InsertAwaitingFundingGraph(db);
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Equal(0,await db.FundingContinuations.CountAsync());
        Assert.Equal(1,await db.GatewayPayments.CountAsync(x=>x.Id==ids.PaymentId));
        Assert.Equal(1,await db.CheckoutSessions.CountAsync(x=>x.Id==ids.CheckoutId));
        await db.GetService<IMigrator>().MigrateAsync(Phase05);
        await using var connection=new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand("SELECT to_regclass('credit_payments.funding_continuations') IS NULL",connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Active_continuation_and_wallet_funding_session_are_database_unique()
    {
        await ResetTo();
        await using var db=Create();
        var ids=await InsertAwaitingFundingGraph(db);
        var first=FundingContinuation.Create(ids.PaymentId,ids.CheckoutId,ids.CustomerId,Guid.NewGuid(),3_000_000,Now);
        db.FundingContinuations.Add(first);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.FundingContinuations.Add(FundingContinuation.Create(ids.PaymentId,ids.CheckoutId,ids.CustomerId,Guid.NewGuid(),3_000_000,Now));
        var active=await Assert.ThrowsAnyAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation,((PostgresException)active.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        first=await db.FundingContinuations.SingleAsync(x=>x.Id==first.Id);
        var fundingSessionId=Guid.NewGuid();
        first.BindSession(fundingSessionId,Now);
        await db.SaveChangesAsync();
        first.MarkFailed(Now.AddMinutes(1));
        await db.SaveChangesAsync();
        var second=FundingContinuation.Create(ids.PaymentId,ids.CheckoutId,ids.CustomerId,Guid.NewGuid(),2_000_000,Now.AddMinutes(2));
        second.BindSession(fundingSessionId,Now.AddMinutes(2));
        db.FundingContinuations.Add(second);
        var session=await Assert.ThrowsAnyAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation,((PostgresException)session.InnerException!).SqlState);
    }

    [Fact]
    public async Task Concurrent_start_claims_one_continuation_and_workers_lease_once()
    {
        await ResetTo();
        await using(var setup=Create())await InsertAwaitingFundingGraph(setup,KnownIds);
        var calls=Enumerable.Range(0,8).Select(_=>Claim()).ToArray();
        var results=await Task.WhenAll(calls);
        Assert.Single(results.Select(x=>x.Continuation!.Id).Distinct());
        Assert.Equal(1,results.Count(x=>x.Outcome==FundingContinuationClaimOutcome.Created));
        var leases=await Task.WhenAll(Lease("worker-a"),Lease("worker-b"));
        Assert.Equal(1,leases.Sum(x=>x.Count));
    }

    [Fact]
    public async Task WalletFunding_commit_before_CPG_binding_recovers_same_continuation_and_session()
    {
        await ResetTo();
        await using(var setup=Create())await InsertAwaitingFundingGraph(setup,KnownIds);
        var first=await Claim();
        var committedWalletFundingSession=Guid.NewGuid();
        var replay=await Claim();
        Assert.Equal(first.Continuation!.Id,replay.Continuation!.Id);
        Assert.Equal(first.Continuation.ClientReference,replay.Continuation.ClientReference);
        await using(var bindDb=Create())
            await new CreditPaymentsRepository(bindDb).BindWalletFundingSessionAsync(
                replay.Continuation.Id,committedWalletFundingSession,Now,default);
        await using var verify=Create();
        var stored=await verify.FundingContinuations.SingleAsync();
        Assert.Equal(committedWalletFundingSession,stored.WalletFundingSessionId);
        Assert.Equal(FundingContinuationStatus.RedirectReady,stored.Status);
    }

    [Fact]
    public async Task Browser_and_worker_terminal_race_converges_to_one_success_state()
    {
        await ResetTo();
        await using(var setup=Create())await InsertAwaitingFundingGraph(setup,KnownIds);
        var claim=await Claim();
        async Task Complete()
        {
            await using var db=Create();
            await new CreditPaymentsRepository(db).CompleteFundingContinuationAsync(
                claim.Continuation!.Id,FundingContinuationStatus.Succeeded,Now,default);
        }
        await Task.WhenAll(Complete(),Complete());
        await using var verify=Create();
        var stored=await verify.FundingContinuations.SingleAsync();
        Assert.Equal(FundingContinuationStatus.Succeeded,stored.Status);
        Assert.Equal(Now,stored.CompletedAt);
    }

    private static readonly (Guid PaymentId,Guid CheckoutId,Guid CustomerId,Guid OrderId) KnownIds=
        (Guid.Parse("10000000-0000-0000-0000-000000000001"),Guid.Parse("20000000-0000-0000-0000-000000000001"),Guid.Parse("30000000-0000-0000-0000-000000000001"),Guid.Parse("40000000-0000-0000-0000-000000000001"));

    private async Task<FundingContinuationClaimResult> Claim()
    {
        await using var db=Create();
        var repository=new CreditPaymentsRepository(db);
        return await repository.CreateOrGetFundingContinuationAsync(KnownIds.CheckoutId,KnownIds.PaymentId,
            KnownIds.CustomerId,KnownIds.OrderId,Guid.Parse("50000000-0000-0000-0000-000000000001"),
            3_000_000,3,Now,default);
    }
    private async Task<IReadOnlyList<Guid>> Lease(string owner)
    {
        await using var db=Create();
        var repository=new CreditPaymentsRepository(db);
        return await repository.LeaseFundingContinuationRecoveryBatchAsync(owner,Now,Now.AddMinutes(2),10,default);
    }
    private async Task ResetTo(string? migration=null)
    {
        await using var db=Create();
        var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync("0");
        await migrator.MigrateAsync(migration);
    }
    private async Task<(Guid PaymentId,Guid CheckoutId,Guid CustomerId,Guid OrderId)> InsertAwaitingFundingGraph(
        CreditPaymentsDbContext db,(Guid PaymentId,Guid CheckoutId,Guid CustomerId,Guid OrderId)? fixedIds=null)
    {
        var ids=fixedIds??(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
        var merchant=MerchantTestFactory.Create(Guid.NewGuid(),"Phase 06 Merchant",Now);merchant.Activate(Now);
        var terminal=merchant.AddTerminal("Phase 06 Terminal","store",100,20_000_000,
            ["https://merchant.example/return"],Now);
        db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var tokenHash=Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).Take(32).ToArray();
        var authHash=Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).Take(32).ToArray();
        var merchantOrderId=$"P6-{ids.PaymentId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO credit_payments.gateway_payments
(gateway_payment_id,merchant_id,terminal_id,merchant_order_id,amount_minor,currency,metadata,status,
 return_url,expires_at,merchant_display_name_snapshot,terminal_name_snapshot,approved_category_code_snapshot,
 customer_id,order_id,order_bound_at,created_at,updated_at,version)
VALUES ({ids.PaymentId},{merchant.Id},{terminal.Id},{merchantOrderId},{10_000_000L},{"IRR"},CAST({"{}"} AS jsonb),
 {(short)GatewayPaymentStatus.AwaitingFunding},{"https://merchant.example/return"},{Now.AddMinutes(15)},
 {"Phase 06 Merchant"},{"Phase 06 Terminal"},{"store"},{ids.CustomerId},{ids.OrderId},{Now},{Now},{Now},{4L});
INSERT INTO credit_payments.checkout_sessions
(checkout_session_id,gateway_payment_id,status,token_nonce,token_hash,token_key_version,expires_at,customer_id,
 authenticated_at,authorization_secret_hash,authorization_expires_at,created_at,updated_at,version)
VALUES ({ids.CheckoutId},{ids.PaymentId},{(short)CheckoutSessionStatus.AwaitingFunding},{new byte[32]},{tokenHash},{1},
 {Now.AddMinutes(15)},{ids.CustomerId},{Now},{authHash},{Now.AddMinutes(15)},{Now},{Now},{4L});");
        return ids;
    }
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
}
