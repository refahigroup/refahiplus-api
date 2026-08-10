using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Features.PayOrder;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;

namespace Refahi.Modules.Orders.Tests;

public sealed class PayOrderCommandHandlerConcurrencyTests
{
    [Fact]
    public async Task Concurrent_requests_with_different_request_keys_create_one_financial_intent()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var order = Order.Create(
            userId,
            "Store",
            null,
            "create-order-key",
            "StoreInPerson",
            [
                new OrderItemData(
                    "خرید حضوری",
                    100_000,
                    1,
                    0,
                    Guid.NewGuid(),
                    "store.in-person",
                    null,
                    null
                ),
            ],
            financialSnapshot: new OrderFinancialSnapshotData(100_000, 0, 0, 0, 0, 100_000),
            paymentPostings:
            [
                new OrderPaymentPostingData(
                    walletId,
                    PaymentPostingDirection.Credit,
                    100_000,
                    "vendor-net"
                ),
            ]
        );
        var repository = new FakeOrderRepository(order);
        var mediator = new PaymentMediator(order.Id, walletId, order.FinalAmountMinor);
        var mutationLock = new SerialOrderMutationLock();
        var cancellationService = new OrderCancellationService(repository, mediator, mediator);
        var handler = new PayOrderCommandHandler(
            repository,
            mediator,
            NullLogger<PayOrderCommandHandler>.Instance,
            mutationLock,
            cancellationService
        );

        PayOrderCommand Command(string requestKey) =>
            new(
                order.Id,
                userId,
                "User",
                [new WalletAllocationInput(walletId, order.FinalAmountMinor)],
                requestKey
            );

        var responses = await Task.WhenAll(
            handler.Handle(Command("customer-checkout-key"), default),
            handler.Handle(Command("vendor-otp-key"), default)
        );

        Assert.All(responses, response => Assert.Equal("Paid", response.Status));
        Assert.Single(responses.Select(x => x.PaymentId).Distinct());
        Assert.Equal(1, mediator.CreateIntentCount);
        Assert.Equal(1, mediator.CaptureCount);
        Assert.Equal($"order-reserve-{order.Id:N}", mediator.ReserveKey);
        Assert.Equal($"order-capture-{order.Id:N}", mediator.CaptureKey);
    }

    private sealed class SerialOrderMutationLock : IOrderMutationLock
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            return new Handle(_semaphore);
        }

        private sealed class Handle(SemaphoreSlim semaphore) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                semaphore.Release();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class PaymentMediator(Guid orderId, Guid walletId, long amountMinor) : IMediator
    {
        private readonly Guid _intentId = Guid.NewGuid();
        private readonly Guid _paymentId = Guid.NewGuid();

        public int CreateIntentCount { get; private set; }
        public int CaptureCount { get; private set; }
        public string? ReserveKey { get; private set; }
        public string? CaptureKey { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default
        )
        {
            object response = request switch
            {
                CreatePaymentIntentCommand command => CreateIntent(command),
                CapturePaymentIntentCommand command => Capture(command),
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)response);
        }

        private CommandResponse<CreatePaymentIntentResponse> CreateIntent(
            CreatePaymentIntentCommand command
        )
        {
            CreateIntentCount++;
            ReserveKey = command.IdempotencyKey;
            return new(
                CommandStatus.Completed,
                new CreatePaymentIntentResponse(
                    _intentId,
                    orderId,
                    amountMinor,
                    "IRR",
                    "Reserved",
                    [new AllocationResponse(walletId, amountMinor)],
                    DateTimeOffset.UtcNow
                )
            );
        }

        private CommandResponse<CapturePaymentIntentResponse> Capture(
            CapturePaymentIntentCommand command
        )
        {
            CaptureCount++;
            CaptureKey = command.IdempotencyKey;
            return new(
                CommandStatus.Completed,
                new CapturePaymentIntentResponse(
                    _paymentId,
                    _intentId,
                    orderId,
                    amountMinor,
                    "IRR",
                    "Paid",
                    [new PaymentAllocationResponse(walletId, amountMinor, Guid.NewGuid())],
                    DateTimeOffset.UtcNow
                )
            );
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakeOrderRepository(Order order) : IOrderRepository
    {
        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default) =>
            Task.FromResult<Order?>(order);

        public Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken ct = default) =>
            Task.FromResult<Order?>(order);

        public Task<Order?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken ct = default
        ) => Task.FromResult<Order?>(null);

        public Task<Order?> GetByIdempotencyKeyWithItemsAsync(
            string idempotencyKey,
            CancellationToken ct = default
        ) => Task.FromResult<Order?>(null);

        public Task<List<Order>> GetByUserIdAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => Task.FromResult<List<Order>>([]);

        public Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<List<Order>> GetAllAsync(
            int page,
            int pageSize,
            string? status,
            Guid? userId,
            string? sourceModule,
            IReadOnlyCollection<Guid>? allowedUserIds = null,
            CancellationToken ct = default
        ) => Task.FromResult<List<Order>>([]);

        public Task<int> CountAllAsync(
            string? status,
            Guid? userId,
            string? sourceModule,
            IReadOnlyCollection<Guid>? allowedUserIds = null,
            CancellationToken ct = default
        ) => Task.FromResult(0);

        public Task<List<Order>> GetBySourceAsync(
            string sourceModule,
            Guid sourceReferenceId,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => Task.FromResult<List<Order>>([]);

        public Task<int> CountBySourceAsync(
            string sourceModule,
            Guid sourceReferenceId,
            CancellationToken ct = default
        ) => Task.FromResult(0);

        public Task AddAsync(Order value, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Order value, CancellationToken ct = default) => Task.CompletedTask;
    }
}
