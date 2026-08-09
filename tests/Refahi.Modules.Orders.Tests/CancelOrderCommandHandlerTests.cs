using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Features.CancelOrder;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Store.Application.Contracts.Vouchers;

namespace Refahi.Modules.Orders.Tests;

public sealed class CancelOrderCommandHandlerTests
{
    [Fact]
    public async Task In_progress_wallet_refund_does_not_mark_order_as_refunded()
    {
        var order = CreatePaidOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.InProgress);
        var handler = CreateHandler(repository, mediator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CancelOrderCommand(order.Id, "provider failure", "refund-key"), default));

        Assert.Equal("بازگشت وجه سفارش هنوز تکمیل نشده است", exception.Message);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(PaymentState.Paid, order.PaymentState);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, mediator.PublishedCount);
    }

    [Fact]
    public async Task Completed_wallet_refund_marks_and_persists_order_as_refunded()
    {
        var order = CreatePaidOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.Completed);
        var handler = CreateHandler(repository, mediator);

        var response = await handler.Handle(
            new CancelOrderCommand(order.Id, "provider failure", "refund-key"), default);

        Assert.Equal("Refunded", response.PaymentAction);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(PaymentState.Refunded, order.PaymentState);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(2, mediator.PublishedCount); // cancellation + settlement refund reversal
    }

    [Fact]
    public async Task Repeated_cancel_after_completed_refund_returns_cached_terminal_result()
    {
        var order = CreatePaidOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.Completed);
        var handler = CreateHandler(repository, mediator);
        var command = new CancelOrderCommand(order.Id, "provider failure", "refund-key");

        await handler.Handle(command, default);
        var repeated = await handler.Handle(command, default);

        Assert.Equal("Refunded", repeated.PaymentAction);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(1, mediator.SendCount);
    }

    [Fact]
    public async Task Redeemed_voucher_guard_blocks_before_any_wallet_refund_mutation()
    {
        var order = CreatePaidStoreOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.Completed, blockVoucherRefund: true);
        var handler = CreateHandler(repository, mediator);

        var ex = await Assert.ThrowsAsync<VoucherApplicationException>(() => handler.Handle(
            new CancelOrderCommand(order.Id, "بازگشت وجه", "refund-key"), default));

        Assert.Equal("REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE", ex.Code);
        Assert.Equal(1, mediator.VoucherGuardSendCount);
        Assert.Equal(0, mediator.WalletRefundSendCount);
        Assert.Equal(PaymentState.Paid, order.PaymentState);
        Assert.Equal(0, repository.UpdateCount);
    }

    private static Order CreatePaidOrder()
    {
        var order = Order.Create(
            Guid.NewGuid(),
            "Charge",
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "ChargeRequest",
            [new OrderItemData("شارژ", 50_000, 1, 0, Guid.NewGuid(), "charge", null, null)]);
        order.MarkAsReserved(Guid.NewGuid());
        order.MarkAsPaid(Guid.NewGuid());
        order.ClearDomainEvents();
        return order;
    }

    private static Order CreatePaidStoreOrder()
    {
        var order = Order.Create(Guid.NewGuid(), "Store", Guid.NewGuid(), Guid.NewGuid().ToString("N"),
            "StoreOrder", [new OrderItemData("ووچر", 50_000, 1, 0, Guid.NewGuid(), "store.voucher", null, null)]);
        order.MarkAsReserved(Guid.NewGuid());
        order.MarkAsPaid(Guid.NewGuid());
        order.ClearDomainEvents();
        return order;
    }

    private static CancelOrderCommandHandler CreateHandler(
        FakeOrderRepository repository,
        RefundMediator mediator) => new(
            repository,
            new ImmediateOrderMutationLock(),
            new OrderCancellationService(repository, mediator, mediator));

    private sealed class ImmediateOrderMutationLock : IOrderMutationLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult<IAsyncDisposable>(new Handle());

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RefundMediator(CommandStatus status, bool blockVoucherRefund = false) : IMediator
    {
        public int PublishedCount { get; private set; }
        public int SendCount { get; private set; }
        public int WalletRefundSendCount { get; private set; }
        public int VoucherGuardSendCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is PrepareStoreOrderRefundCommand prepare)
            {
                VoucherGuardSendCount++;
                if (blockVoucherRefund)
                    throw new VoucherApplicationException("REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE",
                        "به دلیل استفاده شدن ووچر، بازگشت وجه خودکار امکان‌پذیر نیست");
                return Task.FromResult((TResponse)(object)new PrepareStoreOrderRefundResponse(Guid.NewGuid(), 1));
            }
            if (request is not RefundPaymentCommand command)
                throw new NotSupportedException(request.GetType().FullName);

            SendCount++;
            WalletRefundSendCount++;

            var data = status == CommandStatus.Completed
                ? new RefundPaymentResponse(Guid.NewGuid(), command.PaymentId, Guid.NewGuid(), "Completed",
                    50_000, "IRR", [], DateTimeOffset.UtcNow)
                : null;
            object response = new CommandResponse<RefundPaymentResponse>(status, data);
            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishedCount++;
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderRepository(Order order) : IOrderRepository
    {
        public int UpdateCount { get; private set; }
        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult<Order?>(order);
        public Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult<Order?>(order);
        public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) => Task.FromResult<Order?>(null);
        public Task<Order?> GetByIdempotencyKeyWithItemsAsync(string idempotencyKey, CancellationToken ct = default) => Task.FromResult<Order?>(null);
        public Task<List<Order>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct = default) => Task.FromResult<List<Order>>([]);
        public Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<Order>> GetAllAsync(int page, int pageSize, string? status, Guid? userId, string? sourceModule, IReadOnlyCollection<Guid>? allowedUserIds = null, CancellationToken ct = default) => Task.FromResult<List<Order>>([]);
        public Task<int> CountAllAsync(string? status, Guid? userId, string? sourceModule, IReadOnlyCollection<Guid>? allowedUserIds = null, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<Order>> GetBySourceAsync(string sourceModule, Guid sourceReferenceId, int page, int pageSize, CancellationToken ct = default) => Task.FromResult<List<Order>>([]);
        public Task<int> CountBySourceAsync(string sourceModule, Guid sourceReferenceId, CancellationToken ct = default) => Task.FromResult(0);
        public Task AddAsync(Order value, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Order value, CancellationToken ct = default) { UpdateCount++; return Task.CompletedTask; }
    }
}
