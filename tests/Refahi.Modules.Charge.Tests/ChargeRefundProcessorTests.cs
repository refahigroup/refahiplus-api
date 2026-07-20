using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeRefundProcessorTests
{
    [Fact]
    public async Task Failed_refund_is_recoverable_with_the_same_idempotency_key()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CancelOrderRecorder>();
        services.AddMediatR(typeof(ChargeRefundProcessorTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var repository = new FakeChargeRequestRepository();
        var processor = new ChargeRefundProcessor(
            repository,
            provider.GetRequiredService<ISender>(),
            NullLogger<ChargeRefundProcessor>.Instance);
        var recorder = provider.GetRequiredService<CancelOrderRecorder>();
        var request = CreatePaidRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.BeginAsync(request, "عدم انجام شارژ", "stable-refund-key", default));

        Assert.Equal(ChargeRequestStatus.Refunding, request.Status);
        Assert.Equal("stable-refund-key", request.RefundIdempotencyKey);
        Assert.NotNull(request.NextReconciliationAt);

        await processor.ResumeAsync(request, default);

        Assert.Equal(ChargeRequestStatus.Refunded, request.Status);
        Assert.Equal(2, request.RefundAttemptCount);
        Assert.Equal(2, recorder.Commands.Count);
        Assert.All(recorder.Commands, command => Assert.Equal("stable-refund-key", command.IdempotencyKey));
    }

    private static ChargeRequest CreatePaidRequest()
    {
        var now = DateTime.UtcNow;
        var request = ChargeRequest.Create(Guid.NewGuid(), "Eniac", ChargeOperator.Irancell,
            ChargeServiceType.DirectCharge, "09350000000", null, "CUSTOM", "شارژ مستقیم",
            1001, 0, null, 1, "{}", 50_000, null, 0, 0, 0, 50_000,
            Guid.NewGuid().ToString("N"), now, now.AddMinutes(20));
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);
        return request;
    }

    public sealed class CancelOrderRecorder
    {
        public List<CancelOrderCommand> Commands { get; } = [];
        public bool FailNext { get; set; } = true;
    }

    public sealed class CancelOrderHandler(CancelOrderRecorder recorder)
        : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
    {
        public Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            recorder.Commands.Add(request);
            if (recorder.FailNext)
            {
                recorder.FailNext = false;
                throw new InvalidOperationException("temporary wallet failure");
            }

            return Task.FromResult(new CancelOrderResponse(request.OrderId, "Cancelled", "Refunded"));
        }
    }

    private sealed class FakeChargeRequestRepository : IChargeRequestRepository
    {
        public Task<ChargeRequest?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<IReadOnlyList<ChargeRequest>> GetWorkItemsAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([]);
        public Task<IReadOnlyList<ChargeRequest>> GetExpiredCandidatesAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([]);
        public Task AddAsync(ChargeRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddFulfillmentAttemptAsync(ChargeFulfillmentAttempt attempt, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
