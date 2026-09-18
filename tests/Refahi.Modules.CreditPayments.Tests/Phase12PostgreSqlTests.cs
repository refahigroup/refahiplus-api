using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase12PostgreSqlTests : IAsyncLifetime
{
    private const string Phase11 = "20260917190845_CreditPayments_AddRiskAndOperationalControls";
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Phase11_to_phase12_preserves_financial_data_and_rolls_back_new_tables()
    {
        await Reset(Phase11); var ids = await SeedPayment();
        await using (var upgrade = Create()) await upgrade.GetService<IMigrator>().MigrateAsync();
        await using (var verify = Create())
        {
            Assert.NotNull(await verify.GatewayPayments.SingleOrDefaultAsync(x => x.Id == ids.PaymentId));
            Assert.Empty(await verify.ReconciliationRuns.ToListAsync());
            Assert.Empty(await verify.ReconciliationFindings.ToListAsync());
        }
        await using (var rollback = Create()) await rollback.GetService<IMigrator>().MigrateAsync(Phase11);
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync();
        foreach (var table in new[] { "reconciliation_runs", "reconciliation_findings" })
        {
            await using var command = new NpgsqlCommand($"SELECT to_regclass('credit_payments.{table}') IS NULL", connection);
            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task Two_workers_have_one_active_run_owner_per_kind()
    {
        await Reset();
        async Task<ReconciliationRun?> Start(string worker)
        {
            await using var db = Create();
            return await new ReconciliationRepository(db).TryStartRunAsync(ReconciliationKind.PaymentExecution,
                worker, null, null, false, Now.AddMinutes(5), Now, default);
        }
        var results = await Task.WhenAll(Task.Run(() => Start("worker-a")), Task.Run(() => Start("worker-b")));
        Assert.Single(results, x => x is not null);
        await using var verify = Create();
        Assert.Single(await verify.ReconciliationRuns.Where(x => x.Status == ReconciliationRunStatus.Running).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_observation_deduplicates_one_active_finding()
    {
        await Reset(); var ids = await SeedPayment(); var run = await StartRun(ReconciliationKind.InternalIntegrity);
        var observation = Observation(ReconciliationFindingCodes.CheckoutStateMismatch, ReconciliationSeverity.Warning, "{\"state\":\"mismatch\"}");
        async Task Observe()
        {
            await using var db = Create();
            await new ReconciliationRepository(db).ReconcileFindingsAsync(run.Id, ReconciliationKind.InternalIntegrity,
                "GatewayPayment", ids.PaymentId, ids.Version, [observation],
                ReconciliationResolutionCategory.ExternalConvergence, Now, default);
        }
        await Task.WhenAll(Task.Run(Observe), Task.Run(Observe));
        await using var verify = Create(); var finding = await verify.ReconciliationFindings.SingleAsync();
        Assert.Equal(2, finding.ObservationCount); Assert.Equal(ReconciliationFindingStatus.Open, finding.Status);
    }

    [Fact]
    public async Task Recheck_resolves_but_acknowledgement_does_not_resolve()
    {
        await Reset(); var ids = await SeedPayment(); var run = await StartRun(ReconciliationKind.InternalIntegrity);
        await using (var db = Create())
        {
            var repo = new ReconciliationRepository(db);
            await repo.ReconcileFindingsAsync(run.Id, ReconciliationKind.InternalIntegrity, "GatewayPayment",
                ids.PaymentId, ids.Version, [Observation(ReconciliationFindingCodes.CheckoutStateMismatch,
                    ReconciliationSeverity.Warning, "{\"state\":\"mismatch\"}")],
                ReconciliationResolutionCategory.ExternalConvergence, Now, default);
            var findingId = (await db.ReconciliationFindings.SingleAsync()).Id;
            Assert.True(await repo.AcknowledgeFindingAsync(findingId, "admin-1", "بررسی شد", Now.AddMinutes(1), default));
        }
        await using (var verifyAcknowledged = Create())
            Assert.Equal(ReconciliationFindingStatus.Acknowledged, (await verifyAcknowledged.ReconciliationFindings.SingleAsync()).Status);
        await using (var db = Create())
            await new ReconciliationRepository(db).ReconcileFindingsAsync(run.Id, ReconciliationKind.InternalIntegrity,
                "GatewayPayment", ids.PaymentId, ids.Version, [], ReconciliationResolutionCategory.OperationalAction,
                Now.AddMinutes(2), default);
        await using var verify = Create(); var resolved = await verify.ReconciliationFindings.SingleAsync();
        Assert.Equal(ReconciliationFindingStatus.Resolved, resolved.Status); Assert.NotNull(resolved.ResolvedAt);
    }

    [Fact]
    public async Task Canceled_payment_with_authoritative_capture_creates_critical_without_local_rewrite()
    {
        await Reset(); var ids = await SeedPayment();
        await using (var db = Create())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE credit_payments.gateway_payments SET status={(short)GatewayPaymentStatus.Canceled}, updated_at={Now} WHERE gateway_payment_id={ids.PaymentId}");
        var mediator = new OrderStatusMediator(new OrderPaymentExecutionStatusDto(ids.OrderId, ids.CustomerId,
            1_000, "IRR", "CreditPayments", ids.PaymentId, "CreditPayments", "store", "Succeeded",
            Guid.NewGuid(), Guid.NewGuid(), Now));
        await Execute(ReconciliationKind.PaymentExecution, mediator);
        await Execute(ReconciliationKind.PaymentExecution, mediator);
        await using var verify = Create();
        var finding = await verify.ReconciliationFindings.SingleAsync(x => x.FindingCode == ReconciliationFindingCodes.TerminalStateCaptureConflict);
        Assert.Equal(ReconciliationSeverity.Critical, finding.Severity);
        Assert.Equal(2, finding.ObservationCount);
        Assert.Equal(GatewayPaymentStatus.Canceled, (await verify.GatewayPayments.SingleAsync(x => x.Id == ids.PaymentId)).Status);
        Assert.Equal(0, mediator.RecoveryCalls);
    }

    [Fact]
    public async Task Orders_timeout_is_operational_error_not_financial_finding()
    {
        await Reset(); var ids = await SeedPayment();
        await using (var db = Create())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE credit_payments.gateway_payments SET status={(short)GatewayPaymentStatus.Processing}, updated_at={Now} WHERE gateway_payment_id={ids.PaymentId}");
        var run = await Execute(ReconciliationKind.PaymentExecution, new OrderStatusMediator(null, timeout: true));
        Assert.Equal(1, run!.ErrorCount);
        await using var verify = Create(); Assert.Empty(await verify.ReconciliationFindings.ToListAsync());
    }

    [Fact]
    public async Task Risk_counter_drift_is_deterministically_rebuilt_and_finding_resolved()
    {
        await Reset(); var ids = await SeedPayment(); var run = await StartRun(ReconciliationKind.RiskIntegrity);
        Guid counterId;
        await using (var db = Create())
        {
            var counter = RiskVelocityCounter.Create(RiskScopeType.Terminal, ids.TerminalId,
                RiskOperationTypes.PaymentCreate, new DateTimeOffset(Now.Date, TimeSpan.Zero), Now);
            db.RiskVelocityCounters.Add(counter); await db.SaveChangesAsync(); counterId = counter.Id;
            db.RiskReservations.Add(RiskReservation.Create(counter.Id, RiskScopeType.Terminal, ids.TerminalId,
                RiskOperationTypes.PaymentCreate, ids.PaymentId, 1_000, counter.BucketStart, Now));
            await db.SaveChangesAsync();
        }
        await using (var db = Create())
        {
            var result = await new ReconciliationRepository(db).AuditRiskIntegrityAsync(run.Id, 50, true, Now, default);
            Assert.Equal((1, 1, 1), result);
        }
        await using var verify = Create();
        var counterAfter = await verify.RiskVelocityCounters.SingleAsync(x => x.Id == counterId);
        Assert.Equal(1, counterAfter.AcceptedCount); Assert.Equal(1_000, counterAfter.AcceptedAmountMinor);
        Assert.Equal(ReconciliationFindingStatus.Resolved,
            (await verify.ReconciliationFindings.SingleAsync(x => x.FindingCode == ReconciliationFindingCodes.RiskCounterDrift)).Status);
    }

    [Fact]
    public async Task Snapshot_version_change_prevents_stale_finding()
    {
        await Reset(); var ids = await SeedPayment(); var run = await StartRun(ReconciliationKind.PaymentExecution);
        await using (var db = Create())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE credit_payments.gateway_payments SET version=version+1 WHERE gateway_payment_id={ids.PaymentId}");
        await using var reconcile = Create();
        var result = await new ReconciliationRepository(reconcile).ReconcileFindingsAsync(run.Id,
            ReconciliationKind.PaymentExecution, "GatewayPayment", ids.PaymentId, ids.Version,
            [Observation(ReconciliationFindingCodes.PaymentCaptureStateMismatch, ReconciliationSeverity.Critical, "{\"stale\":true}")],
            ReconciliationResolutionCategory.ExternalConvergence, Now, default);
        Assert.False(result.SnapshotCurrent);
        Assert.Empty(await reconcile.ReconciliationFindings.ToListAsync());
    }

    [Fact]
    public async Task Funding_identity_mismatch_is_critical_and_never_rebound_or_recovered()
    {
        await Reset(); var (ids, continuation, sessionId, walletId) = await SeedPendingFunding();
        var mediator = new OrderStatusMediator(null);
        var external = new WalletFundingStatus(sessionId, Guid.NewGuid(), 999, "IRR", "Succeeded", Guid.NewGuid(),
            Guid.NewGuid(), continuation.ClientReference + "-other");
        await Execute(ReconciliationKind.Funding, mediator, new FixedFundingService(external));
        await using var verify = Create();
        Assert.NotEmpty(await verify.ReconciliationFindings.Where(x => x.Severity == ReconciliationSeverity.Critical).ToListAsync());
        var persisted = await verify.FundingContinuations.SingleAsync(x => x.Id == continuation.Id);
        Assert.Equal(sessionId, persisted.WalletFundingSessionId); Assert.Equal(walletId, persisted.PersonalWalletId);
        Assert.Equal(0, mediator.FundingRecoveryCalls);
    }

    [Fact]
    public async Task Matching_authoritative_funding_success_invokes_only_existing_recovery_command()
    {
        await Reset(); var (_, continuation, sessionId, walletId) = await SeedPendingFunding();
        var mediator = new OrderStatusMediator(null);
        var external = new WalletFundingStatus(sessionId, walletId, continuation.RequiredDeficitMinor, "IRR",
            "Succeeded", Guid.NewGuid(), continuation.CustomerId, continuation.ClientReference);
        await Execute(ReconciliationKind.Funding, mediator, new FixedFundingService(external));
        Assert.Equal(1, mediator.FundingRecoveryCalls);
        await using var verify = Create();
        Assert.Single(await verify.ReconciliationFindings.Where(x => x.FindingCode == ReconciliationFindingCodes.FundingStatusStale).ToListAsync());
    }

    private async Task<ReconciliationRunDto?> Execute(ReconciliationKind kind, IMediator mediator,
        IWalletFundingService? funding = null)
    {
        await using var db = Create(); var repository = new ReconciliationRepository(db);
        var options = Options.Create(new ReconciliationOptions { Enabled = true, BatchSize = 50,
            LookbackHours = 24, ProcessingStaleAfterMinutes = 1, FundingStaleAfterMinutes = 1,
            RefundStaleAfterMinutes = 1, ProjectionStaleAfterMinutes = 1, DeadLetterStaleAfterMinutes = 1,
            AutoRemediationEnabled = true, MaxAutoRemediationAttempts = 3, BaseRetrySeconds = 1, MaximumRetrySeconds = 60 });
        var handler = new ReconciliationHandlers(new ReconciliationAdminReader(db, new FixedClock(Now)), repository,
            mediator, funding ?? new NullFundingService(), options, new FixedClock(Now));
        return await handler.Handle(new ExecuteReconciliationRunCommand((short)kind, Now.AddHours(-1), Now.AddHours(1), false, "test-worker"), default);
    }

    private async Task<PaymentIds> SeedPayment()
    {
        await using var db = Create(); var merchant = Merchant.Create(Guid.NewGuid(), "merchant", Now); merchant.Activate(Now);
        var terminal = merchant.AddTerminal("terminal", "store", 1, 10_000, ["https://merchant.example/return"], Now);
        db.Merchants.Add(merchant); await db.SaveChangesAsync();
        var payment = GatewayPayment.Create(merchant.Id, terminal.Id, "phase12-order", 1_000, "IRR", null, "{}",
            "https://merchant.example/return", Now.AddHours(1), merchant.DisplayName, terminal.Name, "store", Now);
        payment.ActivateCheckout(Now); var customer = Guid.NewGuid(); var order = Guid.NewGuid();
        payment.BindCustomer(customer, Now); payment.BindOrder(order, Now);
        var checkoutId = Guid.NewGuid(); var checkout = CheckoutSession.Create(checkoutId, payment.Id, new byte[32],
            payment.Id.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(), 1, Now.AddHours(1), Now);
        checkout.Authenticate(customer, Enumerable.Repeat((byte)3, 32).ToArray(), Now.AddHours(1), Now);
        db.GatewayPayments.Add(payment); db.CheckoutSessions.Add(checkout); await db.SaveChangesAsync();
        return new(payment.Id, order, checkoutId, customer, merchant.Id, terminal.Id, payment.Version);
    }

    private async Task<(PaymentIds Ids, FundingContinuation Continuation, Guid SessionId, Guid WalletId)> SeedPendingFunding()
    {
        var ids = await SeedPayment(); var walletId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        await using var db = Create(); var repository = new CreditPaymentsRepository(db);
        await repository.ApplyFundingEvaluationAsync(ids.CheckoutId, ids.PaymentId, ids.CustomerId, ids.OrderId,
            false, 0, 1_000, Now, default);
        var claim = await repository.CreateOrGetFundingContinuationAsync(ids.CheckoutId, ids.PaymentId,
            ids.CustomerId, ids.OrderId, walletId, 1_000, 3, Now, default);
        await repository.BindWalletFundingSessionAsync(claim.Continuation!.Id, sessionId, Now, default);
        await repository.MarkFundingContinuationPendingAsync(claim.Continuation.Id, Now.AddMinutes(-5), Now, default);
        var continuation = (await repository.GetFundingContinuationContextAsync(claim.Continuation.Id, default))!.Continuation;
        return (ids, continuation, sessionId, walletId);
    }

    private async Task<ReconciliationRun> StartRun(ReconciliationKind kind)
    {
        await using var db = Create();
        return (await new ReconciliationRepository(db).TryStartRunAsync(kind, Guid.NewGuid().ToString("N"),
            null, null, false, Now.AddMinutes(5), Now, default))!;
    }
    private async Task Reset(string? migration = null)
    { await using var db = Create(); var m = db.GetService<IMigrator>(); await m.MigrateAsync("0"); await m.MigrateAsync(migration); }
    private CreditPaymentsDbContext Create() => new(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    private static ReconciliationObservation Observation(string code, ReconciliationSeverity severity, string json) =>
        new(code, severity, json, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
    private sealed record PaymentIds(Guid PaymentId, Guid OrderId, Guid CheckoutId, Guid CustomerId, Guid MerchantId, Guid TerminalId, long Version);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class NullFundingService : IWalletFundingService
    {
        public Task<CreateWalletFundingResult> CreateAsync(CreateWalletFundingRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateWalletFundingResult> CreateBoundAsync(CreateBoundWalletFundingRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WalletFundingStatus?> GetStatusAsync(Guid userId, Guid fundingSessionId, CancellationToken ct = default) => Task.FromResult<WalletFundingStatus?>(null);
        public Task<WalletFundingStatus?> GetStatusByClientReferenceAsync(Guid userId, string clientReference, CancellationToken ct = default) => Task.FromResult<WalletFundingStatus?>(null);
    }

    private sealed class FixedFundingService(WalletFundingStatus status) : IWalletFundingService
    {
        public Task<CreateWalletFundingResult> CreateAsync(CreateWalletFundingRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateWalletFundingResult> CreateBoundAsync(CreateBoundWalletFundingRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WalletFundingStatus?> GetStatusAsync(Guid userId, Guid fundingSessionId, CancellationToken ct = default) => Task.FromResult<WalletFundingStatus?>(status);
        public Task<WalletFundingStatus?> GetStatusByClientReferenceAsync(Guid userId, string clientReference, CancellationToken ct = default) => Task.FromResult<WalletFundingStatus?>(status);
    }

    private sealed class OrderStatusMediator(OrderPaymentExecutionStatusDto? status, bool timeout = false) : IMediator
    {
        public int RecoveryCalls { get; private set; }
        public int FundingRecoveryCalls { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is GetOrderPaymentExecutionStatusQuery)
            {
                if (timeout) throw new TimeoutException();
                return Task.FromResult((TResponse)(object?)status!);
            }
            if (request is RecoverGatewayPaymentCommand) { RecoveryCalls++; return Task.FromResult((TResponse)(object)Unit.Value); }
            if (request is RecoverFundingContinuationCommand) { FundingRecoveryCalls++; return Task.FromResult((TResponse)(object)Unit.Value); }
            throw new NotSupportedException(request.GetType().Name);
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
