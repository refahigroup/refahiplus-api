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
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase11PostgreSqlTests:IAsyncLifetime
{
    private const string Phase10="20260917181809_CreditPayments_AddOutboxAndMerchantWebhooks";
    private static readonly DateTimeOffset Now=new(2026,9,17,20,0,0,TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();

    [Fact(Skip = "Legacy staged migration uses the retired Supplier merchant model; current migration is covered separately.")]
    public async Task Phase10_to_phase11_preserves_data_creates_risk_tables_and_rolls_back()
    {
        await Reset(Phase10);
        await using(var db=Create())
        {
            var merchant=ActiveMerchant(); db.Merchants.Add(merchant); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        }
        await using(var verify=Create())
        {
            Assert.Single(await verify.Merchants.ToListAsync());
            Assert.Empty(await verify.RiskVelocityCounters.ToListAsync());
            Assert.Empty(await verify.RiskReservations.ToListAsync());
            Assert.Empty(await verify.RiskDecisions.ToListAsync());
        }
        await using(var rollback=Create())await rollback.GetService<IMigrator>().MigrateAsync(Phase10);
        await using var connection=new NpgsqlConnection(postgres.GetConnectionString());await connection.OpenAsync();
        foreach(var table in new[]{"risk_velocity_counters","risk_reservations","risk_decisions"})
        {
            await using var command=new NpgsqlCommand($"SELECT to_regclass('credit_payments.{table}') IS NULL",connection);
            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task Concurrent_terminal_limit_has_one_winner_without_overshoot()
    {
        await Reset();var graph=await SeedTerminal();
        async Task<object> Run(string key,string order)
        {
            await using var db=Create();
            try{return await CreatePayment(db,graph,key,order);}
            catch(CreditPaymentsApplicationException ex){return ex;}
        }
        var results=await Task.WhenAll(Task.Run(()=>Run("risk-race-key-a","risk-order-a")),
            Task.Run(()=>Run("risk-race-key-b","risk-order-b")));
        Assert.Single(results.OfType<CreateGatewayPaymentResponse>());
        Assert.Equal("payment_limit_exceeded",Assert.Single(results.OfType<CreditPaymentsApplicationException>()).Code);
        await using var verify=Create();
        var counter=await verify.RiskVelocityCounters.SingleAsync();
        Assert.Equal(1,counter.AcceptedCount);Assert.Equal(100,counter.AcceptedAmountMinor);
        Assert.Single(await verify.RiskReservations.ToListAsync());
        Assert.Single(await verify.RiskDecisions.Where(x=>x.Decision==RiskDecision.Denied).ToListAsync());
    }

    [Fact]
    public async Task Idempotent_retry_does_not_double_count()
    {
        await Reset();var graph=await SeedTerminal();
        await using(var first=Create())await CreatePayment(first,graph,"risk-retry-key","risk-retry-order");
        await using(var second=Create())await CreatePayment(second,graph,"risk-retry-key","risk-retry-order");
        await using var verify=Create();
        Assert.Equal(1,(await verify.RiskVelocityCounters.SingleAsync()).AcceptedCount);
        Assert.Single(await verify.RiskReservations.ToListAsync());
    }

    [Fact]
    public async Task Business_insert_conflict_rolls_back_risk_reservation()
    {
        await Reset();var graph=await SeedTerminal();
        await using(var first=Create())await CreatePayment(first,graph,"risk-conflict-a","same-risk-order",2,200);
        await using(var second=Create())
        {
            var error=await Assert.ThrowsAsync<CreditPaymentsApplicationException>(()=>
                CreatePayment(second,graph,"risk-conflict-b","same-risk-order",2,200));
            Assert.Equal("merchant_order_already_exists",error.Code);
        }
        await using var verify=Create();
        Assert.Equal(1,(await verify.RiskVelocityCounters.SingleAsync()).AcceptedCount);
        Assert.Single(await verify.RiskReservations.ToListAsync());
    }

    [Fact]
    public async Task Definitive_no_effect_releases_customer_payment_reservation()
    {
        await Reset();var graph=await SeedExecutablePayment();
        await using(var db=Create())
        {
            var risk=new RiskReservationRequest(RiskScopeType.Customer,graph.CustomerId,
                RiskOperationTypes.PaymentExecution,graph.PaymentId,500,1,500,
                RiskRuleCodes.CustomerDailyPaymentCount,RiskRuleCodes.CustomerDailyPaymentAmount,
                graph.MerchantId,graph.TerminalId,graph.CustomerId,new DateTimeOffset(Now.Date,TimeSpan.Zero));
            var claim=await new CreditPaymentsRepository(db).ClaimPaymentExecutionAsync(graph.PaymentId,
                PaymentAttemptTrigger.CustomerConfirmation,"policy","1",new string('a',64),Guid.NewGuid(),risk,Now,default);
            Assert.Equal(PaymentExecutionClaimOutcome.Claimed,claim.Outcome);
            await new CreditPaymentsRepository(db).FinalizePaymentNoEffectAsync(
                claim.Context.ActiveAttempt!.Id,"no_effect",Now.AddSeconds(1),default);
        }
        await using var verify=Create();
        Assert.Equal(0,(await verify.RiskVelocityCounters.SingleAsync()).AcceptedCount);
        Assert.NotNull((await verify.RiskReservations.SingleAsync()).ReleasedAt);
    }

    [Fact]
    public async Task Concurrent_customer_execution_limit_has_one_winner()
    {
        await Reset();var graph=await SeedExecutablePayments(2);
        async Task<PaymentExecutionClaimResult> Claim(Guid paymentId,PaymentAttemptTrigger trigger)
        {
            await using var db=Create();
            var risk=new RiskReservationRequest(RiskScopeType.Customer,graph.CustomerId,
                RiskOperationTypes.PaymentExecution,paymentId,600,2,1000,
                RiskRuleCodes.CustomerDailyPaymentCount,RiskRuleCodes.CustomerDailyPaymentAmount,
                graph.MerchantId,graph.TerminalId,graph.CustomerId,new DateTimeOffset(Now.Date,TimeSpan.Zero));
            return await new CreditPaymentsRepository(db).ClaimPaymentExecutionAsync(paymentId,trigger,
                "policy","1",new string('a',64),Guid.NewGuid(),risk,Now,default);
        }
        var claims=await Task.WhenAll(Task.Run(()=>Claim(graph.PaymentIds[0],PaymentAttemptTrigger.CustomerConfirmation)),
            Task.Run(()=>Claim(graph.PaymentIds[1],PaymentAttemptTrigger.FundingAutoResume)));
        Assert.Single(claims,x=>x.Outcome==PaymentExecutionClaimOutcome.Claimed);
        Assert.Single(claims,x=>x.Outcome==PaymentExecutionClaimOutcome.RiskDenied);
        await using var verify=Create();
        var counter=await verify.RiskVelocityCounters.SingleAsync();
        Assert.Equal(1,counter.AcceptedCount);Assert.Equal(600,counter.AcceptedAmountMinor);
    }

    [Fact]
    public async Task Confirm_and_auto_resume_same_payment_count_once()
    {
        await Reset();var graph=await SeedExecutablePayments(1);var paymentId=graph.PaymentIds[0];
        async Task<PaymentExecutionClaimResult> Claim(PaymentAttemptTrigger trigger)
        {
            await using var db=Create();
            var risk=new RiskReservationRequest(RiskScopeType.Customer,graph.CustomerId,
                RiskOperationTypes.PaymentExecution,paymentId,600,10,10_000,
                RiskRuleCodes.CustomerDailyPaymentCount,RiskRuleCodes.CustomerDailyPaymentAmount,
                graph.MerchantId,graph.TerminalId,graph.CustomerId,new DateTimeOffset(Now.Date,TimeSpan.Zero));
            return await new CreditPaymentsRepository(db).ClaimPaymentExecutionAsync(paymentId,trigger,
                "policy","1",new string('a',64),Guid.NewGuid(),risk,Now,default);
        }
        var claims=await Task.WhenAll(Task.Run(()=>Claim(PaymentAttemptTrigger.CustomerConfirmation)),
            Task.Run(()=>Claim(PaymentAttemptTrigger.FundingAutoResume)));
        Assert.Single(claims,x=>x.Outcome==PaymentExecutionClaimOutcome.Claimed);
        Assert.Single(claims,x=>x.Outcome==PaymentExecutionClaimOutcome.Processing);
        await using var verify=Create();
        Assert.Equal(1,(await verify.RiskVelocityCounters.SingleAsync()).AcceptedCount);
        Assert.Single(await verify.RiskReservations.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_refund_limit_has_one_winner_and_failed_release_restores_capacity()
    {
        await Reset();var graph=await SeedCapturedPayments(2);
        async Task<RefundClaimResult> Claim(Guid paymentId,string key)
        {
            await using var db=Create();
            var risk=new RiskReservationRequest(RiskScopeType.Terminal,graph.TerminalId,
                RiskOperationTypes.RefundCreate,Guid.Empty,600,2,1000,
                RiskRuleCodes.TerminalDailyRefundCount,RiskRuleCodes.TerminalDailyRefundAmount,
                graph.MerchantId,graph.TerminalId,null,new DateTimeOffset(Now.Date,TimeSpan.Zero));
            return await new CreditPaymentsRepository(db).ClaimRefundAsync(paymentId,graph.MerchantId,
                graph.TerminalId,Guid.NewGuid(),key,new byte[32],600,null,null,risk,Now,default);
        }
        var claims=await Task.WhenAll(Task.Run(()=>Claim(graph.PaymentIds[0],"refund-risk-a")),
            Task.Run(()=>Claim(graph.PaymentIds[1],"refund-risk-b")));
        var accepted=Assert.Single(claims,x=>x.Outcome==RefundClaimOutcome.Claimed);
        Assert.Single(claims,x=>x.Outcome==RefundClaimOutcome.RiskDenied);
        await using(var db=Create())
            await new CreditPaymentsRepository(db).MarkRefundFailedAsync(accepted.Refund!.Id,"definitive",Now.AddSeconds(1),default);
        var retry=await Claim(graph.PaymentIds.Single(x=>x!=accepted.Payment!.Id),"refund-risk-c");
        Assert.Equal(RefundClaimOutcome.Claimed,retry.Outcome);
        await using var verify=Create();
        var counter=await verify.RiskVelocityCounters.SingleAsync();
        Assert.Equal(1,counter.AcceptedCount);Assert.Equal(600,counter.AcceptedAmountMinor);
    }

    private async Task<TerminalGraph> SeedTerminal()
    {
        await using var db=Create();var merchant=ActiveMerchant();
        var terminal=merchant.AddTerminal("terminal","store",1,10_000,
            ["https://merchant.example/callback"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        return new(merchant.Id,terminal.Id,Guid.Empty,Guid.Empty);
    }

    private async Task<TerminalGraph> SeedExecutablePayment()
    {
        await using var db=Create();var merchant=ActiveMerchant();
        var terminal=merchant.AddTerminal("terminal","store",1,10_000,
            ["https://merchant.example/callback"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var payment=GatewayPayment.Create(merchant.Id,terminal.Id,"execution-order",500,"IRR",null,"{}",
            "https://merchant.example/callback",Now.AddMinutes(10),merchant.DisplayName,terminal.Name,"store",Now);
        var customer=Guid.NewGuid();var checkoutId=Guid.NewGuid();payment.ActivateCheckout(Now);
        payment.BindCustomer(customer,Now);payment.BindOrder(Guid.NewGuid(),Now);
        var session=CheckoutSession.Create(checkoutId,payment.Id,new byte[32],
            payment.Id.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(),1,Now.AddMinutes(10),Now);
        session.Authenticate(customer,Enumerable.Repeat((byte)7,32).ToArray(),Now.AddMinutes(10),Now);
        db.GatewayPayments.Add(payment);db.CheckoutSessions.Add(session);await db.SaveChangesAsync();
        return new(merchant.Id,terminal.Id,payment.Id,customer);
    }

    private async Task<MultiPaymentGraph> SeedExecutablePayments(int count)
    {
        await using var db=Create();var merchant=ActiveMerchant();
        var terminal=merchant.AddTerminal("terminal","store",1,10_000,
            ["https://merchant.example/callback"],Now);db.Merchants.Add(merchant);await db.SaveChangesAsync();
        var customer=Guid.NewGuid();var ids=new List<Guid>();
        for(var i=0;i<count;i++)
        {
            var payment=GatewayPayment.Create(merchant.Id,terminal.Id,$"execution-order-{i}",600,"IRR",null,"{}",
                "https://merchant.example/callback",Now.AddMinutes(10),merchant.DisplayName,terminal.Name,"store",Now);
            var checkoutId=Guid.NewGuid();payment.ActivateCheckout(Now);payment.BindCustomer(customer,Now);
            payment.BindOrder(Guid.NewGuid(),Now);
            var session=CheckoutSession.Create(checkoutId,payment.Id,new byte[32],
                payment.Id.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(),1,Now.AddMinutes(10),Now);
            session.Authenticate(customer,Enumerable.Repeat((byte)7,32).ToArray(),Now.AddMinutes(10),Now);
            db.GatewayPayments.Add(payment);db.CheckoutSessions.Add(session);ids.Add(payment.Id);
        }
        await db.SaveChangesAsync();return new(merchant.Id,terminal.Id,customer,ids.ToArray());
    }

    private async Task<MultiPaymentGraph> SeedCapturedPayments(int count)
    {
        var graph=await SeedExecutablePayments(count);
        foreach(var paymentId in graph.PaymentIds)
        {
            await using var db=Create();var repository=new CreditPaymentsRepository(db);
            var claim=await repository.ClaimPaymentExecutionAsync(paymentId,
                PaymentAttemptTrigger.CustomerConfirmation,"policy","1",new string('a',64),Guid.NewGuid(),Now,default);
            await repository.FinalizePaymentSuccessAsync(claim.Context.ActiveAttempt!.Id,null,
                Guid.NewGuid(),Now,Now,default);
        }
        return graph;
    }

    private async Task<CreateGatewayPaymentResponse> CreatePayment(CreditPaymentsDbContext db,
        TerminalGraph graph,string key,string order,long countLimit=1,long amountLimit=100)
    {
        var checkoutOptions=Options.Create(new CheckoutOptions{PublicBaseUrl="https://pay.example",
            TokenKeys=new(){[1]=Convert.ToBase64String(new byte[32])}});
        var security=new HmacCheckoutSecurityService(checkoutOptions);
        var risk=Options.Create(new RiskOptions{TerminalPayments=new RiskLimitOptions{
            Enabled=true,DailyCountLimit=countLimit,DailyAmountLimitMinor=amountLimit}});
        var repository=new CreditPaymentsRepository(db);
        var handler=new CreateGatewayPaymentHandler(repository,repository,repository,security,
            Options.Create(new GatewayPaymentOptions()),checkoutOptions,risk,new FixedClock(Now));
        return await handler.Handle(new(graph.MerchantId,graph.TerminalId,Guid.NewGuid(),key,order,100,
            "IRR",null,"https://merchant.example/callback",null,null),default);
    }

    private static Merchant ActiveMerchant(){var x=MerchantTestFactory.Create(Guid.NewGuid(),"merchant",Now);x.Activate(Now);return x;}
    private async Task Reset(string? target=null){await using var db=Create();var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("0");await migrator.MigrateAsync(target);}
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
    private sealed record TerminalGraph(Guid MerchantId,Guid TerminalId,Guid PaymentId,Guid CustomerId);
    private sealed record MultiPaymentGraph(Guid MerchantId,Guid TerminalId,Guid CustomerId,Guid[] PaymentIds);
    private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
}
