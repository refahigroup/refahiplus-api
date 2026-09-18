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

public sealed class Phase08PostgreSqlTests : IAsyncLifetime
{
    private const string Phase07="20260917055027_CreditPayments_AddPaymentExecutionAndAttempts";
    private static readonly DateTimeOffset Now=new(2026,9,17,8,0,0,TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Phase07_to_phase08_creates_constraints_preserves_data_and_rolls_back()
    {
        await ResetTo(Phase07);
        var ids=await InsertPhase07Payment();
        await using(var migrate=Create())await migrate.GetService<IMigrator>().MigrateAsync();
        await using(var verify=Create())
        {
            Assert.Equal(ids.PaymentId,(await verify.GatewayPayments.SingleAsync()).Id);
            Assert.Empty(await verify.PaymentTerminations.ToListAsync());
        }
        await using(var rollback=Create())await rollback.GetService<IMigrator>().MigrateAsync(Phase07);
        await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync();
        await using var command=new NpgsqlCommand("SELECT to_regclass('credit_payments.payment_terminations') IS NULL",connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Awaiting_customer_merchant_cancel_is_local_idempotent_and_creates_no_attempt()
    {
        await ResetTo();var ids=await InsertGraph();
        await using var db=Create();var repo=new CreditPaymentsRepository(db);
        var hash=CancelGatewayPaymentHandler.Hash(ids.PaymentId,"merchant_order_canceled");
        var first=await repo.RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
            "cancel-key-001",hash,"merchant_order_canceled",Now,default);
        var repeated=await repo.RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
            "cancel-key-001",hash,"merchant_order_canceled",Now,default);
        Assert.Equal(PaymentTerminationRequestOutcome.Completed,first.Outcome);
        Assert.Equal(PaymentTerminationRequestOutcome.AlreadyCanceled,repeated.Outcome);
        Assert.Equal(GatewayPaymentStatus.Canceled,(await db.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(CheckoutSessionStatus.Canceled,(await db.CheckoutSessions.SingleAsync()).Status);
        Assert.Single(await db.PaymentTerminations.ToListAsync());
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
    }

    [Fact]
    public async Task Customer_cancel_enforces_binding_and_different_keys_converge_to_one_termination()
    {
        await ResetTo();var ids=await InsertGraph();
        await using var db=Create();var repo=new CreditPaymentsRepository(db);
        var notOwner=await repo.RequestCustomerCancellationAsync(ids.CheckoutId,ids.PaymentId,Guid.NewGuid(),
            "customer-key-01",CancelGatewayPaymentHandler.Hash(ids.PaymentId,null),null,Now,default);
        Assert.Equal(PaymentTerminationRequestOutcome.NotFound,notOwner.Outcome);
        var first=await repo.RequestCustomerCancellationAsync(ids.CheckoutId,ids.PaymentId,ids.CustomerId,
            "customer-key-01",CancelGatewayPaymentHandler.Hash(ids.PaymentId,null),null,Now,default);
        var second=await repo.RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
            "merchant-key-02",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"later"),"later",Now,default);
        Assert.Equal(PaymentTerminationRequestOutcome.Completed,first.Outcome);
        Assert.Equal(PaymentTerminationRequestOutcome.AlreadyCanceled,second.Outcome);
        Assert.Single(await db.PaymentTerminations.ToListAsync());
        Assert.Equal(PaymentTerminationSource.Customer,(await db.PaymentTerminations.SingleAsync()).Source);
    }

    [Fact]
    public async Task Same_cancel_key_with_different_reason_conflicts()
    {
        await ResetTo();var ids=await InsertGraph();
        await using var db=Create();var repo=new CreditPaymentsRepository(db);
        await repo.RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
            "cancel-key-003",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"a"),"a",Now,default);
        var conflict=await repo.RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
            "cancel-key-003",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"b"),"b",Now,default);
        Assert.Equal(PaymentTerminationRequestOutcome.IdempotencyConflict,conflict.Outcome);
    }

    [Fact]
    public async Task Due_awaiting_funding_expiry_is_local_and_disables_auto_resume()
    {
        await ResetTo();var ids=await InsertGraph(awaitingFunding:true,withSucceededContinuation:true);
        var due=Now.AddMinutes(2);
        var claims=await Task.WhenAll(ClaimExpiry(due),ClaimExpiry(due));
        Assert.Equal(1,claims.Sum(x=>x.Count));
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Expired,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(CheckoutSessionStatus.Expired,(await verify.CheckoutSessions.SingleAsync()).Status);
        Assert.Single(await verify.PaymentTerminations.ToListAsync());
        var resume=await new CreditPaymentsRepository(verify).LeaseAutomaticResumeBatchAsync(
            "worker",due,due.AddMinutes(1),10,default);
        Assert.Empty(resume);
    }

    [Fact]
    public async Task Historical_created_payment_without_checkout_expires_safely()
    {
        await ResetTo();var ids=await InsertGraph(withCheckout:false);
        await using var db=Create();var claims=await new CreditPaymentsRepository(db)
            .ClaimDueExpirationsAsync(Now.AddMinutes(2),10,default);
        Assert.Single(claims);
        Assert.Equal(GatewayPaymentStatus.Expired,(await db.GatewayPayments.SingleAsync()).Status);
        Assert.Empty(await db.CheckoutSessions.ToListAsync());
    }

    [Fact]
    public async Task Processing_not_started_termination_does_not_replay_payment()
    {
        await ResetTo();var ids=await InsertGraph();
        var attemptId=await ClaimProcessing(ids);
        await RequestCancel(ids);
        var mediator=new TerminationMediator(ids,TerminationMode.NoFinancialEffect);
        await Resolve(ids,mediator);
        Assert.Equal(0,mediator.ExecuteCalls);
        Assert.Equal(1,mediator.TerminateCalls);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Canceled,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentAttemptStatus.Failed,(await verify.PaymentAttempts.SingleAsync(x=>x.Id==attemptId)).Status);
        Assert.Equal(PaymentTerminationStatus.Completed,(await verify.PaymentTerminations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Processing_capture_wins_over_cancel()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        var mediator=new TerminationMediator(ids,TerminationMode.Captured);
        await Resolve(ids,mediator);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(CheckoutSessionStatus.Completed,(await verify.CheckoutSessions.SingleAsync()).Status);
        Assert.Equal(PaymentTerminationStatus.CaptureWon,(await verify.PaymentTerminations.SingleAsync()).Status);
        Assert.Equal(0,mediator.TerminateCalls);
    }

    [Fact]
    public async Task Release_timeout_stays_processing_then_recovery_converges_with_same_reference()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        var mediator=new TerminationMediator(ids,TerminationMode.Timeout);
        await Resolve(ids,mediator);
        await using(var pending=Create())
        {
            Assert.Equal(GatewayPaymentStatus.Processing,(await pending.GatewayPayments.SingleAsync()).Status);
            Assert.Equal(PaymentTerminationStatus.PendingRecovery,(await pending.PaymentTerminations.SingleAsync()).Status);
        }
        mediator.Mode=TerminationMode.Released;
        await Resolve(ids,mediator);
        await using var final=Create();
        Assert.Equal(GatewayPaymentStatus.Canceled,(await final.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(2,mediator.TerminateCalls);
        Assert.All(mediator.ReleaseReferences,x=>Assert.Equal($"cpg-release-{ids.PaymentId:N}",x));
    }

    [Fact]
    public async Task Cancel_and_expiration_race_has_one_durable_winner()
    {
        await ResetTo();var ids=await InsertGraph();
        await Task.WhenAll(RequestCancel(ids),ClaimExpiry(Now.AddMinutes(2)));
        await using var verify=Create();
        Assert.Single(await verify.PaymentTerminations.ToListAsync());
        var payment=await verify.GatewayPayments.SingleAsync();
        Assert.Contains(payment.Status,new[]{GatewayPaymentStatus.Canceled,GatewayPaymentStatus.Expired});
    }

    [Fact]
    public async Task Concurrent_merchant_and_customer_cancels_have_one_business_owner()
    {
        await ResetTo();var ids=await InsertGraph();
        var merchant=Task.Run(async()=>
        {
            await using var db=Create();return await new CreditPaymentsRepository(db)
                .RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
                    "merchant-race-01",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"merchant"),
                    "merchant",Now,default);
        });
        var customer=Task.Run(async()=>
        {
            await using var db=Create();return await new CreditPaymentsRepository(db)
                .RequestCustomerCancellationAsync(ids.CheckoutId,ids.PaymentId,ids.CustomerId,
                    "customer-race-01",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"customer"),
                    "customer",Now,default);
        });
        await Task.WhenAll(merchant,customer);
        await using var verify=Create();
        Assert.Single(await verify.PaymentTerminations.ToListAsync());
        Assert.Equal(GatewayPaymentStatus.Canceled,(await verify.GatewayPayments.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("CustomerConfirmation",false)]
    [InlineData("CustomerConfirmation",true)]
    [InlineData("FundingAutoResume",false)]
    [InlineData("FundingAutoResume",true)]
    public async Task Execution_claim_racing_cancel_or_expiry_never_finishes_captured_and_terminated(
        string trigger,bool expire)
    {
        await ResetTo();var ids=await InsertGraph(withSucceededContinuation:trigger=="FundingAutoResume");
        var mediator=new RacingExecutionMediator(ids);
        var execution=ExecutePayment(ids,mediator,trigger);
        await mediator.ExecutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if(expire)await ClaimExpiry(Now.AddMinutes(2));else await RequestCancel(ids);
        mediator.AllowCapture.SetResult();
        var result=await execution;
        Assert.Equal("succeeded",result.Status);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentTerminationStatus.CaptureWon,(await verify.PaymentTerminations.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execution_definitive_no_capture_racing_termination_finishes_requested_terminal_state(bool expire)
    {
        await ResetTo();var ids=await InsertGraph();
        var mediator=new RacingExecutionMediator(ids,capture:false);
        var clock=new MutableClock(Now);
        var execution=ExecutePayment(ids,mediator,"CustomerConfirmation",clock);
        await mediator.ExecutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if(expire)
        {
            clock.Now=Now.AddMinutes(2);
            await ClaimExpiry(clock.Now);
        }
        else await RequestCancel(ids);
        mediator.AllowCapture.SetResult();
        var result=await execution;
        Assert.Equal(expire?"expired":"canceled",result.Status);
        await using var verify=Create();
        Assert.Equal(expire?GatewayPaymentStatus.Expired:GatewayPaymentStatus.Canceled,
            (await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentTerminationStatus.Completed,(await verify.PaymentTerminations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Pending_termination_recovery_has_one_payment_attempt_lease_owner()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        var claims=await Task.WhenAll(LeaseRecovery("termination-worker-a"),LeaseRecovery("termination-worker-b"));
        Assert.Equal(1,claims.Sum(x=>x.Count));
    }

    [Fact]
    public async Task Durable_termination_survives_pre_release_restart()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        await using(var persisted=Create())
            Assert.Equal(PaymentTerminationStatus.PendingRecovery,
                (await persisted.PaymentTerminations.SingleAsync()).Status);
        var mediator=new TerminationMediator(ids,TerminationMode.Released);
        await Resolve(ids,mediator);
        Assert.Equal(1,mediator.TerminateCalls);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Canceled,(await verify.GatewayPayments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Capture_committed_during_release_wins()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        var mediator=new TerminationMediator(ids,TerminationMode.CaptureDuringRelease);
        await Resolve(ids,mediator);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentTerminationStatus.CaptureWon,(await verify.PaymentTerminations.SingleAsync()).Status);
        Assert.Equal(1,mediator.TerminateCalls);
    }

    [Fact]
    public async Task Release_commit_before_cpg_save_is_recovered_as_already_released()
    {
        await ResetTo();var ids=await InsertGraph();await ClaimProcessing(ids);await RequestCancel(ids);
        var mediator=new TerminationMediator(ids,TerminationMode.AlreadyReleased);
        // Represents an Orders/Wallet release committed before CPG wrote its terminal state.
        await Resolve(ids,mediator);
        await using var verify=Create();
        Assert.Equal(GatewayPaymentStatus.Canceled,(await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Equal(PaymentTerminationStatus.Completed,(await verify.PaymentTerminations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Succeeded_payment_cannot_be_canceled()
    {
        await ResetTo();var ids=await InsertGraph();var attemptId=await ClaimProcessing(ids);
        await using(var successDb=Create())await new CreditPaymentsRepository(successDb)
            .FinalizePaymentSuccessAsync(attemptId,Guid.NewGuid(),Guid.NewGuid(),Now,Now,default);
        await using var db=Create();var result=await new CreditPaymentsRepository(db)
            .RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,"cancel-success-01",
                CancelGatewayPaymentHandler.Hash(ids.PaymentId,null),null,Now,default);
        Assert.Equal(PaymentTerminationRequestOutcome.Succeeded,result.Outcome);
        Assert.Empty(await db.PaymentTerminations.ToListAsync());
    }

    [Fact]
    public void CreditPayments_wallet_access_remains_refund_only_after_webhooks()
    {
        var root=FindRepositoryRoot();
        var files=Directory.EnumerateFiles(Path.Combine(root,"src"),"*.*",SearchOption.AllDirectories)
            .Where(x=>x.Contains("Refahi.Modules.CreditPayments.",StringComparison.Ordinal)
                &&(x.EndsWith(".cs")||x.EndsWith(".csproj")));
        var source=string.Join('\n',files.Select(File.ReadAllText));
        Assert.Contains("RefundPaymentCommand",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".ReserveAsync(",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".CaptureAsync(",source,StringComparison.Ordinal);
        Assert.DoesNotContain(".ReleaseAsync(",source,StringComparison.Ordinal);
        Assert.Contains("OutboxMessage",source,StringComparison.Ordinal);
    }

    private async Task<Guid> ClaimProcessing(Ids ids)
    {
        await using var db=Create();var claim=await new CreditPaymentsRepository(db)
            .ClaimPaymentExecutionAsync(ids.PaymentId,PaymentAttemptTrigger.CustomerConfirmation,
                "policy","1",new string('a',64),Guid.NewGuid(),Now,default);
        return claim.Context.ActiveAttempt!.Id;
    }
    private async Task<PaymentTerminationRequestResult> RequestCancel(Ids ids)
    {
        await using var db=Create();return await new CreditPaymentsRepository(db)
            .RequestMerchantCancellationAsync(ids.PaymentId,ids.MerchantId,ids.TerminalId,
                "cancel-key-008",CancelGatewayPaymentHandler.Hash(ids.PaymentId,"cancel"),"cancel",Now,default);
    }
    private async Task<IReadOnlyList<ExpirationClaim>> ClaimExpiry(DateTimeOffset due)
    {await using var db=Create();return await new CreditPaymentsRepository(db).ClaimDueExpirationsAsync(due,10,default);}
    private async Task<IReadOnlyList<Guid>> LeaseRecovery(string owner)
    {await using var db=Create();return await new CreditPaymentsRepository(db).LeasePaymentRecoveryBatchAsync(
        owner,Now,Now.AddMinutes(1),10,default);}
    private async Task Resolve(Ids ids,TerminationMediator mediator)
    {
        await using var db=Create();await new ResolvePaymentTerminationHandler(new CreditPaymentsRepository(db),mediator,
            new FixedClock(Now.AddMinutes(3)),Options.Create(new PaymentExecutionOptions{BaseRetrySeconds=1,MaximumRetrySeconds=2}),
            NullLogger<ResolvePaymentTerminationHandler>.Instance)
            .Handle(new ResolvePaymentTerminationCommand(ids.PaymentId),default);
    }
    private async Task<CheckoutPaymentExecutionDto> ExecutePayment(Ids ids,IMediator mediator,string trigger,
        TimeProvider? clock=null)
    {
        await using var db=Create();var repo=new CreditPaymentsRepository(db);
        return await new ExecuteGatewayPaymentHandler(repo,repo,new FixedPolicy(Guid.NewGuid()),mediator,
            clock??new FixedClock(Now),Options.Create(new PaymentExecutionOptions()),
            NullLogger<ExecuteGatewayPaymentHandler>.Instance)
            .Handle(new ExecuteGatewayPaymentCommand(ids.PaymentId,trigger),default);
    }

    private async Task<Ids> InsertGraph(bool awaitingFunding=false,bool withSucceededContinuation=false,bool withCheckout=true)
    {
        await using var db=Create();
        var merchant=Merchant.Create(Guid.NewGuid(),"Phase 08 Merchant",Now);merchant.Activate(Now);
        var terminal=merchant.AddTerminal("Phase 08 Terminal","store",100,20_000_000,
            ["https://merchant.example/return"],Now);
        db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var ids=new Ids(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),merchant.Id,terminal.Id);
        var payment=GatewayPayment.Create(merchant.Id,terminal.Id,$"P8-{ids.PaymentId:N}",10_000_000,"IRR",null,"{}",
            "https://merchant.example/return",Now.AddMinutes(1),merchant.DisplayName,terminal.Name,"store",Now);
        typeof(GatewayPayment).GetProperty(nameof(GatewayPayment.Id))!.SetValue(payment,ids.PaymentId);
        if(withCheckout)
        {
            payment.ActivateCheckout(Now);payment.BindCustomer(ids.CustomerId,Now);payment.BindOrder(ids.OrderId,Now);
            var session=CheckoutSession.Create(ids.CheckoutId,payment.Id,new byte[32],
                ids.PaymentId.ToByteArray().Concat(ids.CheckoutId.ToByteArray()).ToArray(),1,Now.AddMinutes(1),Now);
            session.Authenticate(ids.CustomerId,Enumerable.Repeat((byte)8,32).ToArray(),Now.AddMinutes(1),Now);
            if(awaitingFunding){payment.MarkAwaitingFunding(Now);session.MarkAwaitingFunding(Now);}
            db.CheckoutSessions.Add(session);
            if(withSucceededContinuation)
            {
                var continuation=FundingContinuation.Create(payment.Id,session.Id,ids.CustomerId,Guid.NewGuid(),1_000_000,Now);
                continuation.MarkSucceeded(Now);db.FundingContinuations.Add(continuation);
            }
        }
        db.GatewayPayments.Add(payment);await db.SaveChangesAsync();return ids;
    }

    private async Task<Ids> InsertPhase07Payment()
    {
        await using var db=Create();var merchant=Merchant.Create(Guid.NewGuid(),"Phase 07 seed",Now);merchant.Activate(Now);var terminal=merchant.AddTerminal("Old Terminal","store",100,20_000_000,["https://merchant.example/return"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var ids=new Ids(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),merchant.Id,terminal.Id);
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO credit_payments.gateway_payments
            (gateway_payment_id,merchant_id,terminal_id,merchant_order_id,amount_minor,currency,metadata,status,return_url,expires_at,merchant_display_name_snapshot,terminal_name_snapshot,approved_category_code_snapshot,captured_amount_minor,created_at,updated_at,version)
            VALUES ({ids.PaymentId},{merchant.Id},{terminal.Id},{"P7-"+ids.PaymentId.ToString("N")},{10_000_000L},{"IRR"},{"{}"}::jsonb,{(short)GatewayPaymentStatus.Created},{"https://merchant.example/return"},{Now.AddMinutes(10)},{merchant.DisplayName},{terminal.Name},{"store"},{0L},{Now},{Now},{1L})");
        return ids;
    }

    private async Task ResetTo(string? migration=null)
    {await using var db=Create();var m=db.GetService<IMigrator>();await m.MigrateAsync("0");await m.MigrateAsync(migration);}
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
    private static string FindRepositoryRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Refahi.Backend.slnx")))d=d.Parent;return d?.FullName??throw new InvalidOperationException();}
    private sealed record Ids(Guid PaymentId,Guid CheckoutId,Guid CustomerId,Guid OrderId,Guid MerchantId,Guid TerminalId);
    private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
    private sealed class MutableClock(DateTimeOffset now):TimeProvider
    {public DateTimeOffset Now{get;set;}=now;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class FixedPolicy(Guid destination):IMerchantPaymentPolicyResolver
    {public Task<MerchantPaymentPolicy?> ResolveAsync(Guid m,Guid t,string c,long a,string currency,CancellationToken ct)=>Task.FromResult<MerchantPaymentPolicy?>(new("policy","1",new string('f',64),Now.AddDays(-1),c,destination));}
    private enum TerminationMode{NoFinancialEffect,Captured,Released,AlreadyReleased,Timeout,CaptureDuringRelease}
    private sealed class TerminationMediator(Ids ids,TerminationMode mode):IMediator
    {
        public TerminationMode Mode{get;set;}=mode;public int TerminateCalls;public int ExecuteCalls{get;private set;}
        public List<string> ReleaseReferences{get;}=[];private readonly Guid walletPaymentId=Guid.NewGuid();
        private bool captureDuringReleaseCompleted;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object result=request switch
            {
                GetOrderPaymentExecutionStatusQuery=>new OrderPaymentExecutionStatusDto(ids.OrderId,ids.CustomerId,
                    10_000_000,"IRR","CreditPayments",ids.PaymentId,"CreditPayments","store",
                    Mode==TerminationMode.Captured||captureDuringReleaseCompleted?"Succeeded":"NotStarted",
                    Mode==TerminationMode.Captured||captureDuringReleaseCompleted?Guid.NewGuid():null,
                    Mode==TerminationMode.Captured||captureDuringReleaseCompleted?walletPaymentId:null,
                    Mode==TerminationMode.Captured||captureDuringReleaseCompleted?Now:null),
                TerminateOrderPaymentCommand command=>Terminate(command),
                ExecuteOrderPaymentCommand=>throw new InvalidOperationException("PayOrder must not be replayed during termination."),
                _=>throw new NotSupportedException(request.GetType().Name)
            };
            return Task.FromResult((TResponse)result);
        }
        private TerminateOrderPaymentResponse Terminate(TerminateOrderPaymentCommand command)
        {
            Interlocked.Increment(ref TerminateCalls);ReleaseReferences.Add(command.ReleaseReference);
            if(Mode==TerminationMode.Timeout)throw new TimeoutException();
            if(Mode==TerminationMode.CaptureDuringRelease)
            {
                captureDuringReleaseCompleted=true;
                return new(ids.OrderId,"Captured",Guid.NewGuid(),walletPaymentId);
            }
            return new(ids.OrderId,Mode switch{TerminationMode.Released=>"Released",TerminationMode.AlreadyReleased=>"AlreadyReleased",_=>"NoFinancialEffect"});
        }
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default) where TNotification:INotification=>Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }

    private sealed class RacingExecutionMediator(Ids ids,bool capture=true):IMediator
    {
        public TaskCompletionSource ExecutionEntered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowCapture{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Guid intentId=Guid.NewGuid();private readonly Guid paymentId=Guid.NewGuid();private bool captured;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object result;
            switch(request)
            {
                case GetOrderFundingEligibilityQuery:
                    result=new OrderFundingEligibilityDto(ids.OrderId,ids.CustomerId,10_000_000,"IRR",
                        10_000_000,true,0,true,Now,Guid.NewGuid());break;
                case GetOrderPaymentExecutionStatusQuery:
                    result=new OrderPaymentExecutionStatusDto(ids.OrderId,ids.CustomerId,10_000_000,"IRR",
                        "CreditPayments",ids.PaymentId,"CreditPayments","store",captured?"Succeeded":"NotStarted",
                        captured?intentId:null,captured?paymentId:null,captured?Now:null);break;
                case ExecuteOrderPaymentCommand:
                    ExecutionEntered.TrySetResult();await AllowCapture.Task.WaitAsync(ct);
                    if(capture)
                    {
                        captured=true;
                        result=new ExecuteOrderPaymentResponse(ids.OrderId,"Succeeded",paymentId,Now);
                    }
                    else result=new ExecuteOrderPaymentResponse(ids.OrderId,"InsufficientFunds",null,null,
                        "insufficient_funds");
                    break;
                default:throw new NotSupportedException(request.GetType().Name);
            }
            return (TResponse)result;
        }
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default) where TNotification:INotification=>Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
