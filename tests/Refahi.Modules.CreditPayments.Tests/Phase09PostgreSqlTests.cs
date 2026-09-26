using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MediatR;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase09PostgreSqlTests:IAsyncLifetime
{
    private const string Phase08="20260917084159_CreditPayments_AddCancellationAndExpiration";
    private static readonly DateTimeOffset Now=new(2026,9,17,11,0,0,TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();

    [Fact(Skip = "Legacy staged migration uses the retired Supplier merchant model; current migration is covered separately.")]public async Task Phase08_to_phase09_preserves_payment_adds_zero_total_and_rolls_back()
    {
        await Reset(Phase08);var ids=await InsertPhase08Captured();
        await using(var db=Create())await db.GetService<IMigrator>().MigrateAsync();
        await using(var verify=Create()){var p=await verify.GatewayPayments.SingleAsync();Assert.Equal(ids.Payment,p.Id);Assert.Equal(0,p.RefundedAmountMinor);Assert.Empty(await verify.Refunds.ToListAsync());}
        await using(var db=Create())await db.GetService<IMigrator>().MigrateAsync(Phase08);
        await using var c=new NpgsqlConnection(postgres.GetConnectionString());await c.OpenAsync();
        await using var q=new NpgsqlCommand("SELECT to_regclass('credit_payments.refunds') IS NULL",c);Assert.True((bool)(await q.ExecuteScalarAsync())!);
    }

    [Fact]public async Task Concurrent_over_refund_accepts_at_most_one_reservation()
    {await Reset();var ids=await InsertCaptured();var x=await Task.WhenAll(Claim(ids,6_000_000,"key-over-001"),Claim(ids,6_000_000,"key-over-002"));Assert.Equal(1,x.Count(v=>v==RefundClaimOutcome.Claimed));Assert.Equal(1,x.Count(v=>v==RefundClaimOutcome.AmountExceeded));}

    [Fact]public async Task Concurrent_exact_split_reserves_both_without_exceeding_capture()
    {await Reset();var ids=await InsertCaptured();var x=await Task.WhenAll(Claim(ids,4_000_000,"key-split-01"),Claim(ids,6_000_000,"key-split-02"));Assert.All(x,v=>Assert.Equal(RefundClaimOutcome.Claimed,v));await using var db=Create();Assert.Equal(10_000_000,await db.Refunds.SumAsync(r=>r.AmountMinor));}

    [Fact]public async Task Pending_recovery_reserves_capacity_and_definitive_failure_releases_it()
    {
        await Reset();var ids=await InsertCaptured();var a=await ClaimResult(ids,7_000_000,"key-pending-1");
        await using(var db=Create())await new CreditPaymentsRepository(db).MarkRefundPendingRecoveryAsync(a.Refund!.Id,Now,Now.AddMinutes(1),default);
        Assert.Equal(RefundClaimOutcome.AmountExceeded,await Claim(ids,4_000_000,"key-pending-2"));
        await using(var db=Create())await new CreditPaymentsRepository(db).MarkRefundFailedAsync(a.Refund!.Id,"definitive",Now,default);
        Assert.Equal(RefundClaimOutcome.Claimed,await Claim(ids,10_000_000,"key-pending-3"));
    }

    [Fact]public async Task Multiple_refunds_update_payment_to_partial_then_full_exactly_once()
    {
        await Reset();var ids=await InsertCaptured();long total=0;
        foreach(var amount in new long[]{2_000_000,3_000_000,5_000_000})
        {var claim=await ClaimResult(ids,amount,$"key-multi-{amount}");await using var db=Create();var ctx=await new CreditPaymentsRepository(db).FinalizeRefundSuccessAsync(claim.Refund!.Id,Guid.NewGuid(),Now,Now,default);total+=amount;Assert.Equal(total,ctx.Payment.RefundedAmountMinor);}
        await using var verify=Create();Assert.Equal(GatewayPaymentStatus.Refunded,(await verify.GatewayPayments.SingleAsync()).Status);
    }

    [Fact]public async Task Same_key_replays_same_refund_and_different_payload_conflicts()
    {await Reset();var ids=await InsertCaptured();var a=await ClaimResult(ids,2_000_000,"key-replay-01");var b=await ClaimResult(ids,2_000_000,"key-replay-01");Assert.Equal(RefundClaimOutcome.Existing,b.Outcome);Assert.Equal(a.Refund!.Id,b.Refund!.Id);Assert.Equal(RefundClaimOutcome.IdempotencyConflict,await Claim(ids,3_000_000,"key-replay-01"));}

    [Fact]public async Task Concurrent_merchant_reference_has_one_owner_without_exception_leak()
    {await Reset();var ids=await InsertCaptured();var x=await Task.WhenAll(Claim(ids,2_000_000,"key-ref-001","RF-1"),Claim(ids,2_000_000,"key-ref-002","RF-1"));Assert.Equal(1,x.Count(v=>v==RefundClaimOutcome.Claimed));Assert.Equal(1,x.Count(v=>v==RefundClaimOutcome.MerchantReferenceConflict));}

    [Fact]public async Task Recovery_workers_lease_due_refund_once()
    {await Reset();var ids=await InsertCaptured();await Claim(ids,2_000_000,"key-lease-001");var leased=await Task.WhenAll(Lease("worker-a"),Lease("worker-b"));Assert.Equal(1,leased.Sum(x=>x.Count));}

    [Fact]public async Task Cross_merchant_cannot_claim_read_or_list_refunds()
    {
        await Reset();var ids=await InsertCaptured();var claim=await ClaimResult(ids,2_000_000,"key-idor-001");var other=Guid.NewGuid();
        await using var db=Create();var repo=new CreditPaymentsRepository(db);
        var denied=await repo.ClaimRefundAsync(ids.Payment,other,ids.Terminal,Guid.NewGuid(),"key-idor-002",Hash(ids.Payment,1_000_000,null),1_000_000,null,null,Now,default);
        Assert.Equal(RefundClaimOutcome.NotFound,denied.Outcome);Assert.Null(await repo.GetRefundAsync(claim.Refund!.Id,other,ids.Terminal,default));
        Assert.Null(await new ListRefundsHandler(repo,repo).Handle(new(other,ids.Terminal,ids.Payment),default));
    }

    [Fact]public async Task Wallet_success_then_local_save_failure_recovers_with_same_wallet_key_once_logically()
    {
        await Reset();var ids=await InsertCaptured();var claim=await ClaimResult(ids,2_000_000,"key-save-fail");var wallet=new FakeWallet(claim.Refund!.Id);
        await using(var db=Create()){var actual=new CreditPaymentsRepository(db);var failing=new FailFirstFinalizeRepository(actual);var handler=Handler(failing,wallet);var pending=await handler.Handle(new ProcessRefundCommand(claim.Refund.Id),default);Assert.True(failing.Failed);Assert.Equal("pending_recovery",pending!.Status);}
        await using(var db=Create()){var actual=new CreditPaymentsRepository(db);var result=await Handler(actual,wallet).Handle(new ProcessRefundCommand(claim.Refund.Id),default);Assert.Equal("succeeded",result!.Status);}
        Assert.Equal(2,wallet.Keys.Count);Assert.Single(wallet.Keys.Distinct());
        await using var verify=Create();Assert.Equal(2_000_000,(await verify.GatewayPayments.SingleAsync()).RefundedAmountMinor);Assert.Single(await verify.Refunds.Where(x=>x.Status==RefundStatus.Succeeded).ToListAsync());
    }

    [Fact]public async Task Wallet_timeout_stays_pending_then_idempotent_retry_converges()
    {
        await Reset();var ids=await InsertCaptured();var claim=await ClaimResult(ids,3_000_000,"key-timeout-01");var wallet=new FakeWallet(claim.Refund!.Id){ThrowTimeout=true};
        await using(var db=Create()){var result=await Handler(new CreditPaymentsRepository(db),wallet).Handle(new ProcessRefundCommand(claim.Refund.Id),default);Assert.Equal("pending_recovery",result!.Status);}
        wallet.ThrowTimeout=false;
        await using(var db=Create()){var result=await Handler(new CreditPaymentsRepository(db),wallet).Handle(new ProcessRefundCommand(claim.Refund.Id),default);Assert.Equal("succeeded",result!.Status);}
        Assert.Single(wallet.Keys.Distinct());
    }

    [Fact]public async Task Committed_claim_before_wallet_call_is_recovered_with_original_immutable_request()
    {
        await Reset();var ids=await InsertCaptured();var claim=await ClaimResult(ids,4_000_000,"key-pre-call");var wallet=new FakeWallet(claim.Refund!.Id);
        await using var db=Create();var result=await Handler(new CreditPaymentsRepository(db),wallet).Handle(new ProcessRefundCommand(claim.Refund.Id),default);
        Assert.Equal("succeeded",result!.Status);Assert.Equal($"cpg-refund-{claim.Refund.Id:N}",Assert.Single(wallet.Keys));
    }

    [Fact]public async Task Order_projection_failure_never_downgrades_full_refund_and_retry_does_not_call_wallet_again()
    {
        await Reset();var ids=await InsertCaptured();var claim=await ClaimResult(ids,10_000_000,"key-projection");var wallet=new FakeWallet(claim.Refund!.Id);
        await using(var db=Create()){var handler=new ProcessRefundHandler(new CreditPaymentsRepository(db),wallet,new ProjectionMediator(true),Options.Create(new RefundOptions()),new FixedClock(Now));var result=await handler.Handle(new ProcessRefundCommand(claim.Refund.Id),default);Assert.Equal("succeeded",result!.Status);}
        await using(var verify=Create()){Assert.Equal(GatewayPaymentStatus.Refunded,(await verify.GatewayPayments.SingleAsync()).Status);Assert.Equal(OrderRefundProjectionStatus.Pending,(await verify.Refunds.SingleAsync()).OrderProjectionStatus);}
        await using(var db=Create()){var handler=new ProcessRefundHandler(new CreditPaymentsRepository(db),wallet,new ProjectionMediator(false),Options.Create(new RefundOptions()),new FixedClock(Now.AddMinutes(1)));await handler.Handle(new ProcessRefundCommand(claim.Refund.Id),default);}
        Assert.Single(wallet.Keys);await using var done=Create();Assert.Equal(OrderRefundProjectionStatus.Completed,(await done.Refunds.SingleAsync()).OrderProjectionStatus);
    }

    private async Task<RefundClaimOutcome> Claim(Ids ids,long amount,string key,string? reference=null)=>(await ClaimResult(ids,amount,key,reference)).Outcome;
    private async Task<RefundClaimResult> ClaimResult(Ids ids,long amount,string key,string? reference=null)
    {await using var db=Create();return await new CreditPaymentsRepository(db).ClaimRefundAsync(ids.Payment,ids.Merchant,ids.Terminal,Guid.NewGuid(),key,Hash(ids.Payment,amount,reference),amount,reference,null,Now,default);}
    private async Task<IReadOnlyList<Guid>> Lease(string owner)
    {await using var db=Create();return await new CreditPaymentsRepository(db).LeaseRefundRecoveryBatchAsync(owner,Now,Now.AddMinutes(1),10,default);}
    private async Task<Ids> InsertCaptured()
    {
        await using var db=Create();var merchant=MerchantTestFactory.Create(Guid.NewGuid(),"Phase 09",Now);merchant.Activate(Now);var terminal=merchant.AddTerminal("Refund Terminal","store",100,20_000_000,["https://merchant.example/return"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var customer=Guid.NewGuid();var order=Guid.NewGuid();var payment=GatewayPayment.Create(merchant.Id,terminal.Id,$"P9-{Guid.NewGuid():N}",10_000_000,"IRR",null,"{}","https://merchant.example/return",Now.AddMinutes(10),merchant.DisplayName,terminal.Name,"store",Now);payment.ActivateCheckout(Now);payment.BindCustomer(customer,Now);payment.BindOrder(order,Now);payment.BeginProcessing("policy","1",new string('a',64),Now);payment.MarkSucceeded(Guid.NewGuid(),Now,Now);db.GatewayPayments.Add(payment);await db.SaveChangesAsync();return new(payment.Id,merchant.Id,terminal.Id);
    }
    private async Task<Ids> InsertPhase08Captured()
    {
        await using var db=Create();var merchant=MerchantTestFactory.Create(Guid.NewGuid(),"Phase 08 seed",Now);merchant.Activate(Now);var terminal=merchant.AddTerminal("Old Terminal","store",100,20_000_000,["https://merchant.example/return"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var payment=Guid.NewGuid();var customer=Guid.NewGuid();var order=Guid.NewGuid();var walletPayment=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO credit_payments.gateway_payments
            (gateway_payment_id,merchant_id,terminal_id,merchant_order_id,amount_minor,currency,metadata,status,return_url,expires_at,merchant_display_name_snapshot,terminal_name_snapshot,approved_category_code_snapshot,customer_id,order_id,order_bound_at,captured_amount_minor,wallet_payment_id,paid_at,policy_id,policy_version,policy_hash,created_at,updated_at,version)
            VALUES ({payment},{merchant.Id},{terminal.Id},{"P8-"+payment.ToString("N")},{10_000_000L},{"IRR"},{"{}"}::jsonb,{(short)GatewayPaymentStatus.Succeeded},{"https://merchant.example/return"},{Now.AddMinutes(10)},{merchant.DisplayName},{terminal.Name},{"store"},{customer},{order},{Now},{10_000_000L},{walletPayment},{Now},{"policy"},{"1"},{new string('a',64)},{Now},{Now},{1L})");
        return new(payment,merchant.Id,terminal.Id);
    }
    private async Task Reset(string? target=null){await using var db=Create();var m=db.GetService<IMigrator>();await m.MigrateAsync("0");await m.MigrateAsync(target);}
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
    private static ProcessRefundHandler Handler(IRefundRepository repo,IWalletPaymentExecutor wallet)=>new(repo,wallet,new NoopMediator(),Options.Create(new RefundOptions{MaximumBackoffSeconds=30}),new FixedClock(Now));
    private static byte[] Hash(Guid payment,long amount,string? reference){var fields=new[]{"cpg-create-refund-v1",payment.ToString("D"),amount.ToString(CultureInfo.InvariantCulture),reference??"",""};var canonical=string.Concat(fields.Select(x=>$"{Encoding.UTF8.GetByteCount(x)}:{x}"));return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));}
    private sealed record Ids(Guid Payment,Guid Merchant,Guid Terminal);
    private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
    private sealed class FakeWallet(Guid refundId):IWalletPaymentExecutor
    {
        public bool ThrowTimeout{get;set;}public List<string> Keys{get;}=[];
        public Task<CommandResponse<RefundPaymentResponse>> RefundAsync(RefundPaymentCommand c,CancellationToken ct){Keys.Add(c.IdempotencyKey);if(ThrowTimeout)throw new TimeoutException();return Task.FromResult(new CommandResponse<RefundPaymentResponse>(CommandStatus.Completed,new(refundId,c.PaymentId,Guid.NewGuid(),"Completed",c.AmountMinor!.Value,"IRR",[],Now)));}
        public Task<CommandResponse<CreatePaymentIntentResponse>> ReserveAsync(CreatePaymentIntentCommand c,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CommandResponse<CapturePaymentIntentResponse>> CaptureAsync(CapturePaymentIntentCommand c,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CommandResponse<ReleasePaymentIntentResponse>> ReleaseAsync(ReleasePaymentIntentCommand c,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class FailFirstFinalizeRepository(IRefundRepository inner):IRefundRepository
    {
        public bool Failed{get;private set;}
        public Task<RefundClaimResult> ClaimRefundAsync(Guid a,Guid b,Guid c,Guid d,string e,byte[] f,long g,string? h,string? i,DateTimeOffset j,CancellationToken k)=>inner.ClaimRefundAsync(a,b,c,d,e,f,g,h,i,j,k);
        public Task<Refund?> GetRefundAsync(Guid a,Guid b,Guid c,CancellationToken d)=>inner.GetRefundAsync(a,b,c,d);
        public Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid a,Guid b,Guid c,CancellationToken d)=>inner.ListRefundsAsync(a,b,c,d);
        public Task<RefundContext?> GetRefundContextAsync(Guid a,CancellationToken b)=>inner.GetRefundContextAsync(a,b);
        public Task<RefundContext> FinalizeRefundSuccessAsync(Guid a,Guid b,DateTimeOffset c,DateTimeOffset d,CancellationToken e){if(!Failed){Failed=true;throw new DbUpdateException("simulated local save failure");}return inner.FinalizeRefundSuccessAsync(a,b,c,d,e);}
        public Task MarkRefundFailedAsync(Guid a,string b,DateTimeOffset c,CancellationToken d)=>inner.MarkRefundFailedAsync(a,b,c,d);
        public Task MarkRefundPendingRecoveryAsync(Guid a,DateTimeOffset b,DateTimeOffset c,CancellationToken d)=>inner.MarkRefundPendingRecoveryAsync(a,b,c,d);
        public Task<IReadOnlyList<Guid>> LeaseRefundRecoveryBatchAsync(string a,DateTimeOffset b,DateTimeOffset c,int d,CancellationToken e)=>inner.LeaseRefundRecoveryBatchAsync(a,b,c,d,e);
        public Task CompleteOrderRefundProjectionAsync(Guid a,DateTimeOffset b,CancellationToken c)=>inner.CompleteOrderRefundProjectionAsync(a,b,c);
        public Task ScheduleOrderRefundProjectionAsync(Guid a,DateTimeOffset b,DateTimeOffset c,CancellationToken d)=>inner.ScheduleOrderRefundProjectionAsync(a,b,c,d);
        public Task RecordRefundAuditAsync(Guid a,string b,DateTimeOffset c,CancellationToken d)=>inner.RecordRefundAuditAsync(a,b,c,d);
    }
    private sealed class NoopMediator:IMediator
    {public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default)where TNotification:INotification=>Task.CompletedTask;public IAsyncEnumerable<TResponse>CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();public IAsyncEnumerable<object?>CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();}
    private sealed class ProjectionMediator(bool fail):IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default){if(request is not MarkOrderFullyRefundedCommand command)throw new NotSupportedException();if(fail)throw new InvalidOperationException("orders unavailable");object result=new MarkOrderFullyRefundedResponse(command.OrderId,"Refunded");return Task.FromResult((TResponse)result);}
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default)where TNotification:INotification=>Task.CompletedTask;public IAsyncEnumerable<TResponse>CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();public IAsyncEnumerable<object?>CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
