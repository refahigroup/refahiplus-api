using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase07PostgreSqlTests : IAsyncLifetime
{
    private const string Phase06="20260916204020_CreditPayments_AddWalletFundingContinuation";
    private static readonly DateTimeOffset Now=new(2026,9,17,5,0,0,TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Phase06_to_phase07_preserves_payment_creates_constraints_and_rolls_back()
    {
        await ResetTo(Phase06);
        var ids=await InsertPhase06ReadyGraph();
        await using(var migrate=Create())await migrate.GetService<IMigrator>().MigrateAsync();
        await using(var verify=Create())
        {
            var payment=await verify.GatewayPayments.SingleAsync(x=>x.Id==ids.PaymentId);
            Assert.Equal(0,payment.CapturedAmountMinor);
            Assert.Empty(verify.PaymentAttempts);
        }
        await using(var rollback=Create())await rollback.GetService<IMigrator>().MigrateAsync(Phase06);
        await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync();
        await using var command=new NpgsqlCommand("SELECT to_regclass('credit_payments.payment_attempts') IS NULL",connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Concurrent_confirm_claims_create_one_active_attempt_and_one_execution()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        var calls=Enumerable.Range(0,8).Select(_=>Execute(ids,mediator,"CustomerConfirmation"));
        var results=await Task.WhenAll(calls);
        Assert.Equal(1,mediator.ExecuteCalls);
        Assert.All(results,x=>Assert.Contains(x.Status,new[]{"processing","succeeded"}));
        await using var verify=Create();
        Assert.Single(await verify.PaymentAttempts.ToListAsync());
        var payment=await verify.GatewayPayments.SingleAsync(x=>x.Id==ids.PaymentId);
        Assert.Equal(GatewayPaymentStatus.Succeeded,payment.Status);
        Assert.Equal(payment.AmountMinor,payment.CapturedAmountMinor);
    }

    [Fact]
    public async Task Customer_confirmation_happy_path_persists_authoritative_capture_and_policy_snapshot()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        var result=await Execute(ids,mediator,"CustomerConfirmation");

        Assert.Equal("succeeded",result.Status);
        Assert.Equal(Now,result.PaidAt);
        Assert.Equal("https://merchant.example/return",result.ReturnUrl);
        Assert.Equal(1,mediator.ExecuteCalls);
        Assert.Equal(ids.DestinationWalletId,mediator.LastDestinationWalletId);
        Assert.Equal($"cpg-pay-{ids.PaymentId:N}",mediator.LastIdempotencyKey);
        await using var verify=Create();
        var payment=await verify.GatewayPayments.SingleAsync(x=>x.Id==ids.PaymentId);
        var checkout=await verify.CheckoutSessions.SingleAsync(x=>x.GatewayPaymentId==ids.PaymentId);
        var attempt=await verify.PaymentAttempts.SingleAsync(x=>x.GatewayPaymentId==ids.PaymentId);
        Assert.Equal(GatewayPaymentStatus.Succeeded,payment.Status);
        Assert.Equal(CheckoutSessionStatus.Completed,checkout.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded,attempt.Status);
        Assert.Equal("policy",attempt.PolicyId);
        Assert.Equal("1",attempt.PolicyVersion);
        Assert.Equal(new string('a',64),attempt.PolicyHash);
        Assert.Equal(payment.WalletPaymentId,attempt.WalletPaymentId);
    }

    [Fact]
    public async Task Confirm_and_auto_resume_race_converge_to_one_execution()
    {
        await ResetTo();
        var ids=await InsertReadyGraph(withSucceededContinuation:true);
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        var results=await Task.WhenAll(
            Execute(ids,mediator,"CustomerConfirmation"),
            Execute(ids,mediator,"FundingAutoResume"));
        Assert.Equal(1,mediator.ExecuteCalls);
        Assert.Contains(results,x=>x.Status=="succeeded");
    }

    [Fact]
    public async Task Durable_auto_resume_readiness_executes_without_second_customer_confirmation()
    {
        await ResetTo();
        var ids=await InsertReadyGraph(withSucceededContinuation:true);
        var leased=await LeaseResume("worker-after-restart");
        Assert.Equal([ids.PaymentId],leased);
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        var result=await Execute(ids,mediator,"FundingAutoResume");
        Assert.Equal("succeeded",result.Status);
        Assert.Equal(1,mediator.ExecuteCalls);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(CheckoutSessionStatus.Completed,(await verify.CheckoutSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Unknown_outcome_stays_processing_and_recovery_observes_capture()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.Timeout);
        var first=await Execute(ids,mediator,"CustomerConfirmation");
        Assert.Equal("processing",first.Status);
        await using(var verify=Create())
        {
            Assert.Equal(GatewayPaymentStatus.Processing,(await verify.GatewayPayments.SingleAsync()).Status);
            Assert.Equal(PaymentAttemptStatus.PendingRecovery,(await verify.PaymentAttempts.SingleAsync()).Status);
        }
        mediator.Mode=ExecutionMode.AlreadySucceeded;
        await using(var recoverDb=Create())
        {
            var attemptId=(await recoverDb.PaymentAttempts.AsNoTracking().SingleAsync()).Id;
            var handler=new RecoverGatewayPaymentHandler(new CreditPaymentsRepository(recoverDb),mediator,
                new FixedClock(Now.AddMinutes(1)),Options.Create(new PaymentExecutionOptions()),
                NullLogger<RecoverGatewayPaymentHandler>.Instance);
            await handler.Handle(new RecoverGatewayPaymentCommand(attemptId),default);
        }
        await using var final=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await final.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded,(await final.PaymentAttempts.SingleAsync()).Status);
        Assert.Equal(1,mediator.ExecuteCalls);
    }

    [Fact]
    public async Task Processing_claim_before_orders_call_is_replayed_once_by_recovery()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        Guid attemptId;
        await using(var claimDb=Create())
        {
            var claim=await new CreditPaymentsRepository(claimDb).ClaimPaymentExecutionAsync(ids.PaymentId,
                PaymentAttemptTrigger.CustomerConfirmation,"policy","1",new string('a',64),ids.DestinationWalletId,Now,default);
            attemptId=claim.Context.ActiveAttempt!.Id;
        }
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        await using(var recoverDb=Create())
        {
            var handler=new RecoverGatewayPaymentHandler(new CreditPaymentsRepository(recoverDb),mediator,
                new FixedClock(Now.AddMinutes(1)),Options.Create(new PaymentExecutionOptions()),
                NullLogger<RecoverGatewayPaymentHandler>.Instance);
            await handler.Handle(new RecoverGatewayPaymentCommand(attemptId),default);
        }
        Assert.Equal(1,mediator.ExecuteCalls);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Insufficient_execution_returns_to_awaiting_funding_without_poisoning_payment()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var result=await Execute(ids,new ExecutionMediator(ids,ExecutionMode.Insufficient),"CustomerConfirmation");
        Assert.Equal("awaiting_funding",result.Status);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.AwaitingFunding,(await verify.GatewayPayments.SingleAsync()).Status);
        var attempt=await verify.PaymentAttempts.SingleAsync();
        Assert.Equal(PaymentAttemptStatus.Failed,attempt.Status);
        Assert.Equal("insufficient_funds",attempt.NormalizedOutcomeCode);
    }

    [Fact]
    public async Task Initial_insufficient_confirmation_does_not_claim_or_call_orders_payment()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.InitialInsufficient);
        var result=await Execute(ids,mediator,"CustomerConfirmation");
        Assert.Equal("awaiting_funding",result.Status);
        Assert.NotNull(result.Funding);
        Assert.Equal(2_000_000,result.Funding!.DeficitAmountMinor);
        Assert.Equal(0,mediator.ExecuteCalls);
        await using var verify=Create();
        Assert.Empty(await verify.PaymentAttempts.ToListAsync());
        Assert.Equal(GatewayPaymentStatus.AwaitingFunding,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(CheckoutSessionStatus.AwaitingFunding,(await verify.CheckoutSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Recovery_and_auto_resume_leases_are_single_owner()
    {
        await ResetTo();
        var ids=await InsertReadyGraph(withSucceededContinuation:true);
        await using(var claimDb=Create())
        {
            var repo=new CreditPaymentsRepository(claimDb);
            var claim=await repo.ClaimPaymentExecutionAsync(ids.PaymentId,PaymentAttemptTrigger.CustomerConfirmation,
                "policy","1",new string('b',64),ids.DestinationWalletId,Now,default);
            await repo.MarkPaymentPendingRecoveryAsync(claim.Context.ActiveAttempt!.Id,Now,Now,default);
        }
        var recovery=await Task.WhenAll(LeaseRecovery("a"),LeaseRecovery("b"));
        Assert.Equal(1,recovery.Sum(x=>x.Count));

        await ResetTo();
        await InsertReadyGraph(withSucceededContinuation:true);
        var resume=await Task.WhenAll(LeaseResume("a"),LeaseResume("b"));
        Assert.Equal(1,resume.Sum(x=>x.Count));
    }

    [Fact]
    public async Task Missing_policy_fails_closed_without_attempt()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        await using var db=Create();
        var handler=CreateHandler(db,new ExecutionMediator(ids,ExecutionMode.Success),new MissingPolicyResolver());
        var result=await handler.Handle(new ExecuteGatewayPaymentCommand(ids.PaymentId,"CustomerConfirmation"),default);
        Assert.Equal("payment_policy_unavailable",result.OutcomeCode);
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer,(await db.GatewayPayments.SingleAsync()).Status);
    }

    [Fact]
    public void Confirmation_contract_accepts_no_financial_policy_or_allocation_fields()
    {
        var fields=typeof(ConfirmCheckoutPaymentCommand).GetProperties().Select(x=>x.Name).ToArray();
        Assert.Equal(["Token","CheckoutAuthorizationSecret"],fields);
        var forbidden=new[]{"Destination","Posting","Allocation","Fee","Vat","Category"};
        Assert.DoesNotContain(fields,name=>forbidden.Any(x=>name.Contains(x,StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Recovery_uses_original_policy_snapshot_after_configuration_changes()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        Guid attemptId;
        await using(var claimDb=Create())
        {
            var repo=new CreditPaymentsRepository(claimDb);
            var claim=await repo.ClaimPaymentExecutionAsync(ids.PaymentId,
                PaymentAttemptTrigger.CustomerConfirmation,"policy-original","v1",new string('c',64),
                ids.DestinationWalletId,Now,default);
            attemptId=claim.Context.ActiveAttempt!.Id;
            await repo.MarkPaymentPendingRecoveryAsync(attemptId,Now,Now,default);
        }
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        await using(var recoverDb=Create())
        {
            await new RecoverGatewayPaymentHandler(new CreditPaymentsRepository(recoverDb),mediator,
                new FixedClock(Now.AddMinutes(1)),Options.Create(new PaymentExecutionOptions()),
                NullLogger<RecoverGatewayPaymentHandler>.Instance)
                .Handle(new RecoverGatewayPaymentCommand(attemptId),default);
        }
        Assert.Equal(ids.DestinationWalletId,mediator.LastDestinationWalletId);
        await using var verify=Create();
        var attempt=await verify.PaymentAttempts.SingleAsync();
        Assert.Equal("policy-original",attempt.PolicyId);
        Assert.Equal("v1",attempt.PolicyVersion);
        Assert.Equal(new string('c',64),attempt.PolicyHash);
    }

    [Fact]
    public async Task Order_integrity_mismatch_prevents_financial_execution()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.IntegrityMismatch);
        await using var db=Create();
        var exception=await Assert.ThrowsAsync<CreditPaymentsApplicationException>(()=>
            CreateHandler(db,mediator,new FixedPolicyResolver(ids.DestinationWalletId))
                .Handle(new ExecuteGatewayPaymentCommand(ids.PaymentId,"CustomerConfirmation"),default));
        Assert.Equal("payment_context_invalid",exception.Code);
        Assert.Equal(0,mediator.ExecuteCalls);
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
    }

    [Fact]
    public async Task Wallet_payment_id_cannot_be_bound_to_two_gateway_payments()
    {
        await ResetTo();
        var first=await InsertReadyGraph();
        var second=await InsertReadyGraph();
        var sharedWalletPaymentId=Guid.NewGuid();
        await using(var firstDb=Create())
        {
            var repo=new CreditPaymentsRepository(firstDb);
            var claim=await repo.ClaimPaymentExecutionAsync(first.PaymentId,PaymentAttemptTrigger.CustomerConfirmation,
                "policy","1",new string('d',64),first.DestinationWalletId,Now,default);
            await repo.FinalizePaymentSuccessAsync(claim.Context.ActiveAttempt!.Id,Guid.NewGuid(),sharedWalletPaymentId,Now,Now,default);
        }
        await using var secondDb=Create();
        var secondRepo=new CreditPaymentsRepository(secondDb);
        var secondClaim=await secondRepo.ClaimPaymentExecutionAsync(second.PaymentId,PaymentAttemptTrigger.CustomerConfirmation,
            "policy","1",new string('e',64),second.DestinationWalletId,Now,default);
        await Assert.ThrowsAsync<DbUpdateException>(()=>secondRepo.FinalizePaymentSuccessAsync(
            secondClaim.Context.ActiveAttempt!.Id,Guid.NewGuid(),sharedWalletPaymentId,Now,Now,default));
    }

    [Fact]
    public async Task Repeated_recovery_of_already_succeeded_payment_is_a_no_op()
    {
        await ResetTo();
        var ids=await InsertReadyGraph();
        var mediator=new ExecutionMediator(ids,ExecutionMode.Success);
        await Execute(ids,mediator,"CustomerConfirmation");
        Guid attemptId;
        await using(var db=Create())attemptId=(await db.PaymentAttempts.SingleAsync()).Id;
        for(var i=0;i<2;i++)
        {
            await using var recoveryDb=Create();
            await new RecoverGatewayPaymentHandler(new CreditPaymentsRepository(recoveryDb),mediator,
                new FixedClock(Now.AddMinutes(i+1)),Options.Create(new PaymentExecutionOptions()),
                NullLogger<RecoverGatewayPaymentHandler>.Instance)
                .Handle(new RecoverGatewayPaymentCommand(attemptId),default);
        }
        Assert.Equal(1,mediator.ExecuteCalls);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
    }

    [Fact]
    public void CreditPayments_direct_wallet_boundary_is_refund_only()
    {
        var assemblyReferences=typeof(ExecuteGatewayPaymentHandler).Assembly.GetReferencedAssemblies()
            .Select(x=>x.Name).ToArray();
        Assert.Contains("Refahi.Modules.Wallets.Application.Contracts",assemblyReferences);
        Assert.DoesNotContain("Refahi.Modules.Wallets.Domain",assemblyReferences);
        Assert.DoesNotContain("Refahi.Modules.Wallets.Infrastructure",assemblyReferences);
        var root=FindRepositoryRoot();
        var files=Directory.EnumerateFiles(Path.Combine(root,"src"),"*.*",SearchOption.AllDirectories)
            .Where(path=>path.Contains("Refahi.Modules.CreditPayments.",StringComparison.Ordinal)
                && (path.EndsWith(".cs",StringComparison.Ordinal)||path.EndsWith(".csproj",StringComparison.Ordinal)));
        var source=string.Join('\n',files.Select(File.ReadAllText));
        Assert.Contains("RefundAsync",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".ReserveAsync(",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".CaptureAsync(",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".ReleaseAsync(",source,StringComparison.Ordinal);
    }

    private async Task<CheckoutPaymentExecutionDto> Execute(TestIds ids,ExecutionMediator mediator,string trigger)
    {
        await using var db=Create();
        return await CreateHandler(db,mediator,new FixedPolicyResolver(ids.DestinationWalletId))
            .Handle(new ExecuteGatewayPaymentCommand(ids.PaymentId,trigger),default);
    }
    private ExecuteGatewayPaymentHandler CreateHandler(CreditPaymentsDbContext db,IMediator mediator,IMerchantPaymentPolicyResolver policy)=>
        new(new CreditPaymentsRepository(db),new CreditPaymentsRepository(db),policy,mediator,
            new FixedClock(Now),Options.Create(new PaymentExecutionOptions{BaseRetrySeconds=1,MaximumRetrySeconds=2}),
            NullLogger<ExecuteGatewayPaymentHandler>.Instance);
    private async Task<IReadOnlyList<Guid>> LeaseRecovery(string owner)
    {await using var db=Create();return await new CreditPaymentsRepository(db).LeasePaymentRecoveryBatchAsync(owner,Now,Now.AddMinutes(2),10,default);}
    private async Task<IReadOnlyList<Guid>> LeaseResume(string owner)
    {await using var db=Create();return await new CreditPaymentsRepository(db).LeaseAutomaticResumeBatchAsync(owner,Now,Now.AddMinutes(2),10,default);}

    private static string FindRepositoryRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Refahi.Backend.slnx")))
            directory=directory.Parent;
        return directory?.FullName??throw new InvalidOperationException("Repository root was not found.");
    }

    private async Task<TestIds> InsertReadyGraph(bool withSucceededContinuation=false)
    {
        await using var db=Create();
        var ids=new TestIds(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
        var merchant=Merchant.Create(ids.SupplierId,"Phase 07 Merchant",Now);merchant.Activate(Now);
        var terminal=merchant.AddTerminal("Phase 07 Terminal","store",100,20_000_000,["https://merchant.example/return"],Now);
        db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var payment=GatewayPayment.Create(merchant.Id,terminal.Id,$"P7-{ids.PaymentId:N}",10_000_000,"IRR",null,"{}",
            "https://merchant.example/return",Now.AddMinutes(15),merchant.DisplayName,terminal.Name,"store",Now);
        typeof(GatewayPayment).GetProperty(nameof(GatewayPayment.Id))!.SetValue(payment,ids.PaymentId);
        payment.ActivateCheckout(Now);payment.BindCustomer(ids.CustomerId,Now);payment.BindOrder(ids.OrderId,Now);
        var tokenHash=ids.PaymentId.ToByteArray().Concat(ids.CheckoutId.ToByteArray()).ToArray();
        var session=CheckoutSession.Create(ids.CheckoutId,payment.Id,new byte[32],tokenHash,1,Now.AddMinutes(15),Now);
        session.Authenticate(ids.CustomerId,Enumerable.Repeat((byte)9,32).ToArray(),Now.AddMinutes(15),Now);
        db.GatewayPayments.Add(payment);db.CheckoutSessions.Add(session);await db.SaveChangesAsync();
        if(withSucceededContinuation)
        {
            var continuation=FundingContinuation.Create(payment.Id,session.Id,ids.CustomerId,ids.PersonalWalletId,1_000_000,Now);
            continuation.MarkSucceeded(Now);
            db.FundingContinuations.Add(continuation);await db.SaveChangesAsync();
        }
        return ids with{TerminalId=terminal.Id};
    }
    private async Task<TestIds> InsertPhase06ReadyGraph()
    {
        await using var db=Create();
        var merchant=Merchant.Create(Guid.NewGuid(),"Migration Merchant",Now);merchant.Activate(Now);
        var terminal=merchant.AddTerminal("Migration Terminal","store",100,20_000_000,["https://merchant.example/return"],Now);
        db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var ids=new TestIds(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),terminal.Id,merchant.SupplierId,Guid.NewGuid());
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO credit_payments.gateway_payments
(gateway_payment_id,merchant_id,terminal_id,merchant_order_id,amount_minor,currency,metadata,status,return_url,expires_at,
 merchant_display_name_snapshot,terminal_name_snapshot,approved_category_code_snapshot,customer_id,order_id,order_bound_at,created_at,updated_at,version)
VALUES ({ids.PaymentId},{merchant.Id},{terminal.Id},{"migration-p7"},{1000L},{"IRR"},CAST({"{}"} AS jsonb),{(short)GatewayPaymentStatus.AwaitingCustomer},
 {"https://merchant.example/return"},{Now.AddMinutes(15)},{"Merchant"},{"Terminal"},{"store"},{ids.CustomerId},{ids.OrderId},{Now},{Now},{Now},{3L});
INSERT INTO credit_payments.checkout_sessions
(checkout_session_id,gateway_payment_id,status,token_nonce,token_hash,token_key_version,expires_at,customer_id,authenticated_at,
 authorization_secret_hash,authorization_expires_at,created_at,updated_at,version)
VALUES ({ids.CheckoutId},{ids.PaymentId},{(short)CheckoutSessionStatus.Authenticated},{new byte[32]},{Enumerable.Repeat((byte)1,32).ToArray()},1,
 {Now.AddMinutes(15)},{ids.CustomerId},{Now},{Enumerable.Repeat((byte)2,32).ToArray()},{Now.AddMinutes(15)},{Now},{Now},{3L});");
        return ids;
    }
    private async Task ResetTo(string? migration=null)
    {await using var db=Create();var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("0");await migrator.MigrateAsync(migration);}
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);

    private sealed record TestIds(Guid PaymentId,Guid CheckoutId,Guid CustomerId,Guid OrderId,Guid TerminalId,Guid SupplierId,Guid DestinationWalletId)
    {public Guid PersonalWalletId{get;}=Guid.NewGuid();}
    private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
    private sealed class FixedPolicyResolver(Guid destination):IMerchantPaymentPolicyResolver
    {public Task<MerchantPaymentPolicy?> ResolveAsync(Guid m,Guid t,string c,long a,string currency,CancellationToken ct)=>Task.FromResult<MerchantPaymentPolicy?>(new("policy","1",new string('a',64),Now.AddDays(-1),c,destination));}
    private sealed class MissingPolicyResolver:IMerchantPaymentPolicyResolver
    {public Task<MerchantPaymentPolicy?> ResolveAsync(Guid m,Guid t,string c,long a,string currency,CancellationToken ct)=>Task.FromResult<MerchantPaymentPolicy?>(null);}
    private enum ExecutionMode{Success,Timeout,Insufficient,InitialInsufficient,AlreadySucceeded,IntegrityMismatch}
    private sealed class ExecutionMediator(TestIds ids,ExecutionMode initial):IMediator
    {
        public ExecutionMode Mode{get;set;}=initial;public int ExecuteCalls;
        public Guid? LastDestinationWalletId{get;private set;}
        public string? LastIdempotencyKey{get;private set;}
        private readonly Guid paymentId=Guid.NewGuid();private readonly Guid intentId=Guid.NewGuid();
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object result=request switch
            {
                GetOrderFundingEligibilityQuery=>Mode==ExecutionMode.InitialInsufficient
                    ?new OrderFundingEligibilityDto(ids.OrderId,ids.CustomerId,10_000_000,"IRR",8_000_000,false,2_000_000,true,Now,ids.PersonalWalletId)
                    :new OrderFundingEligibilityDto(ids.OrderId,ids.CustomerId,10_000_000,"IRR",10_000_000,true,0,true,Now,ids.PersonalWalletId),
                GetOrderPaymentExecutionStatusQuery=>Status(),
                ExecuteOrderPaymentCommand command=>Execute(command),
                _=>throw new NotSupportedException(request.GetType().Name)
            };
            return Task.FromResult((TResponse)result);
        }
        private OrderPaymentExecutionStatusDto Status()=>new(ids.OrderId,ids.CustomerId,10_000_000,"IRR","CreditPayments",
            Mode==ExecutionMode.IntegrityMismatch?Guid.NewGuid():ids.PaymentId,
            "CreditPayments","store",Mode==ExecutionMode.AlreadySucceeded||Volatile.Read(ref ExecuteCalls)>0&&Mode==ExecutionMode.Success?"Succeeded":"NotStarted",
            Mode==ExecutionMode.AlreadySucceeded||Volatile.Read(ref ExecuteCalls)>0?intentId:null,
            Mode==ExecutionMode.AlreadySucceeded||Volatile.Read(ref ExecuteCalls)>0&&Mode==ExecutionMode.Success?paymentId:null,
            Mode==ExecutionMode.AlreadySucceeded||Volatile.Read(ref ExecuteCalls)>0&&Mode==ExecutionMode.Success?Now:null);
        private ExecuteOrderPaymentResponse Execute(ExecuteOrderPaymentCommand command)
        {
            Interlocked.Increment(ref ExecuteCalls);
            LastDestinationWalletId=command.DestinationWalletId;
            LastIdempotencyKey=command.IdempotencyKey;
            return Mode switch
            {
                ExecutionMode.Timeout=>throw new TimeoutException(),
                ExecutionMode.Insufficient=>new(ids.OrderId,"InsufficientFunds",null,null,"insufficient_funds"),
                _=>new(ids.OrderId,"Succeeded",paymentId,Now)
            };
        }
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default) where TNotification:INotification=>Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
