using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Features.TerminateOrderPayment;
using Refahi.Modules.Orders.Application.Features.MarkOrderFullyRefunded;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;

namespace Refahi.Modules.Orders.Tests;

public sealed class TerminateOrderPaymentCommandHandlerTests
{
    [Fact]
    public async Task Unpaid_order_terminates_without_wallet_effect()
    {
        var order=CreateOrder();var wallet=new FakeWallet();var repo=new FakeRepository(order);
        var result=await Handler(repo,wallet).Handle(new(order.Id,order.UserId,"cpg-release-payment"),default);
        Assert.Equal("NoFinancialEffect",result.TerminationStatus);
        Assert.Equal(OrderStatus.Cancelled,order.Status);
        Assert.Equal(0,wallet.ReleaseCalls);
    }

    [Fact]
    public async Task Reserved_order_releases_with_caller_stable_identity()
    {
        var order=CreateOrder();var intentId=Guid.NewGuid();order.MarkAsReserved(intentId);
        var wallet=new FakeWallet();var repo=new FakeRepository(order);
        var result=await Handler(repo,wallet).Handle(new(order.Id,order.UserId,"cpg-release-stable"),default);
        Assert.Equal("Released",result.TerminationStatus);
        Assert.Equal(PaymentState.Released,order.PaymentState);
        Assert.Equal("cpg-release-stable",wallet.LastReleaseKey);
        Assert.Equal(intentId,wallet.LastIntentId);
    }

    [Fact]
    public async Task Captured_order_reports_capture_and_never_refunds()
    {
        var order=CreateOrder();order.MarkAsReserved(Guid.NewGuid());order.MarkAsPaid(Guid.NewGuid());
        var wallet=new FakeWallet();var repo=new FakeRepository(order);
        var result=await Handler(repo,wallet).Handle(new(order.Id,order.UserId,"cpg-release-stable"),default);
        Assert.Equal("Captured",result.TerminationStatus);
        Assert.Equal(0,wallet.ReleaseCalls);
        Assert.Equal(0,wallet.RefundCalls);
        Assert.Equal(PaymentState.Paid,order.PaymentState);
    }

    [Fact]
    public async Task Full_refund_projection_is_idempotent_and_validates_original_wallet_payment()
    {
        var order=CreateOrder();order.MarkAsReserved(Guid.NewGuid());var paymentId=Guid.NewGuid();order.MarkAsPaid(paymentId);
        var repo=new FakeRepository(order);var handler=new MarkOrderFullyRefundedCommandHandler(repo,new ImmediateLock());
        var first=await handler.Handle(new(order.Id,paymentId,"cpg-order-refunded-stable"),default);
        var replay=await handler.Handle(new(order.Id,paymentId,"cpg-order-refunded-stable"),default);
        Assert.Equal("Refunded",first.Status);Assert.Equal("AlreadyRefunded",replay.Status);Assert.Equal(PaymentState.Refunded,order.PaymentState);
    }

    [Fact]
    public async Task Full_refund_projection_rejects_mismatched_wallet_payment()
    {
        var order=CreateOrder();order.MarkAsReserved(Guid.NewGuid());order.MarkAsPaid(Guid.NewGuid());
        var handler=new MarkOrderFullyRefundedCommandHandler(new FakeRepository(order),new ImmediateLock());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>handler.Handle(new(order.Id,Guid.NewGuid(),"cpg-order-refunded-stable"),default));
        Assert.Equal(PaymentState.Paid,order.PaymentState);
    }

    private static TerminateOrderPaymentCommandHandler Handler(FakeRepository repo,FakeWallet wallet)=>
        new(repo,new ImmediateLock(),wallet);
    private static Order CreateOrder()=>Order.Create(Guid.NewGuid(),"CreditPayments",Guid.NewGuid(),
        Guid.NewGuid().ToString("N"),"CreditPayments",
        [new OrderItemData("پرداخت",1000,1,0,Guid.NewGuid(),"store",null,null)]);

    private sealed class ImmediateLock:IOrderMutationLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid orderId,CancellationToken ct)=>
            Task.FromResult<IAsyncDisposable>(new Handle());
        private sealed class Handle:IAsyncDisposable{public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
    private sealed class FakeWallet:IWalletPaymentExecutor
    {
        public int ReleaseCalls;public int RefundCalls;public string? LastReleaseKey;public Guid? LastIntentId;
        public Task<CommandResponse<ReleasePaymentIntentResponse>> ReleaseAsync(ReleasePaymentIntentCommand c,CancellationToken ct)
        {ReleaseCalls++;LastReleaseKey=c.IdempotencyKey;LastIntentId=c.IntentId;return Task.FromResult(new CommandResponse<ReleasePaymentIntentResponse>(CommandStatus.Completed,new(c.IntentId,Guid.NewGuid(),"Released",DateTimeOffset.UtcNow)));}
        public Task<CommandResponse<CreatePaymentIntentResponse>> ReserveAsync(CreatePaymentIntentCommand c,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CommandResponse<CapturePaymentIntentResponse>> CaptureAsync(CapturePaymentIntentCommand c,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CommandResponse<RefundPaymentResponse>> RefundAsync(RefundPaymentCommand c,CancellationToken ct){RefundCalls++;throw new NotSupportedException();}
    }
    private sealed class FakeRepository(Order order):IOrderRepository
    {
        public Task<Order?> GetByIdAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<Order?>(order);
        public Task<Order?> GetByIdWithItemsAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<Order?>(order);
        public Task<Order?> GetByIdempotencyKeyAsync(string key,CancellationToken ct=default)=>Task.FromResult<Order?>(null);
        public Task<Order?> GetByIdempotencyKeyWithItemsAsync(string key,CancellationToken ct=default)=>Task.FromResult<Order?>(null);
        public Task<List<Order>> GetByUserIdAsync(Guid id,int page,int size,CancellationToken ct=default)=>Task.FromResult<List<Order>>([]);
        public Task<int> CountByUserIdAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(0);
        public Task<List<Order>> GetAllAsync(int page,int size,string? status,Guid? user,string? module,IReadOnlyCollection<Guid>? allowed=null,CancellationToken ct=default)=>Task.FromResult<List<Order>>([]);
        public Task<int> CountAllAsync(string? status,Guid? user,string? module,IReadOnlyCollection<Guid>? allowed=null,CancellationToken ct=default)=>Task.FromResult(0);
        public Task<List<Order>> GetBySourceAsync(string module,Guid id,int page,int size,CancellationToken ct=default)=>Task.FromResult<List<Order>>([]);
        public Task<int> CountBySourceAsync(string module,Guid id,CancellationToken ct=default)=>Task.FromResult(0);
        public Task AddAsync(Order value,CancellationToken ct=default)=>Task.CompletedTask;
        public Task UpdateAsync(Order value,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
