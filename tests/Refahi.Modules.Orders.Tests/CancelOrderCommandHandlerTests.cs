using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Features.CancelOrder;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

namespace Refahi.Modules.Orders.Tests;

public sealed class CancelOrderCommandHandlerTests
{
    [Fact]
    public async Task In_progress_wallet_refund_does_not_mark_order_as_refunded()
    {
        var order = CreatePaidOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.InProgress);
        var handler = new CancelOrderCommandHandler(repository, mediator, mediator);

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
        var handler = new CancelOrderCommandHandler(repository, mediator, mediator);

        var response = await handler.Handle(
            new CancelOrderCommand(order.Id, "provider failure", "refund-key"), default);

        Assert.Equal("Refunded", response.PaymentAction);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(PaymentState.Refunded, order.PaymentState);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(1, mediator.PublishedCount);
    }

    [Fact]
    public async Task Repeated_cancel_after_completed_refund_returns_cached_terminal_result()
    {
        var order = CreatePaidOrder();
        var repository = new FakeOrderRepository(order);
        var mediator = new RefundMediator(CommandStatus.Completed);
        var handler = new CancelOrderCommandHandler(repository, mediator, mediator);
        var command = new CancelOrderCommand(order.Id, "provider failure", "refund-key");

        await handler.Handle(command, default);
        var repeated = await handler.Handle(command, default);

        Assert.Equal("Refunded", repeated.PaymentAction);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(1, mediator.SendCount);
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

    private sealed class RefundMediator(CommandStatus status) : IMediator
    {
        public int PublishedCount { get; private set; }
        public int SendCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is not RefundPaymentCommand command)
                throw new NotSupportedException(request.GetType().FullName);

            SendCount++;

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
