using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeFulfillmentRecoveryTests
{
    [Fact]
    public async Task Unexpected_failure_after_claim_releases_lease_and_queues_trace()
    {
        var request = CreatePaidRequest();
        var repository = new FakeChargeRequestRepository(request);
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddMediatR(typeof(ChargeFulfillmentRecoveryTests).Assembly)
            .BuildServiceProvider();
        var refunds = new ChargeRefundProcessor(
            repository,
            services.GetRequiredService<ISender>(),
            NullLogger<ChargeRefundProcessor>.Instance);
        var processor = new ChargeFulfillmentProcessor(
            repository,
            new ThrowingProviderResolver(),
            new PassthroughProtector(),
            services.GetRequiredService<IMediator>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ChargeFulfillmentProcessor>.Instance,
            refunds);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(request.Id, CancellationToken.None));

        Assert.Equal(ChargeRequestStatus.ReconciliationPending, request.Status);
        Assert.Null(request.ProcessingLeaseUntil);
        Assert.Null(request.ProcessingLeaseOwner);
        Assert.NotNull(request.NextReconciliationAt);
        Assert.Contains("استعلام مجدد", request.ProviderMessage);
        Assert.True(repository.SaveCount >= 2);
    }

    [Fact]
    public async Task Cancellation_after_claim_releases_lease_and_queues_trace()
    {
        var request = CreatePaidRequest();
        var repository = new FakeChargeRequestRepository(request);
        using var cancellation = new CancellationTokenSource();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddMediatR(typeof(ChargeFulfillmentRecoveryTests).Assembly)
            .BuildServiceProvider();
        var refunds = new ChargeRefundProcessor(
            repository,
            services.GetRequiredService<ISender>(),
            NullLogger<ChargeRefundProcessor>.Instance);
        var processor = new ChargeFulfillmentProcessor(
            repository,
            new CancellingProviderResolver(cancellation),
            new PassthroughProtector(),
            services.GetRequiredService<IMediator>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ChargeFulfillmentProcessor>.Instance,
            refunds);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(request.Id, cancellation.Token));

        Assert.Equal(ChargeRequestStatus.ReconciliationPending, request.Status);
        Assert.Null(request.ProcessingLeaseUntil);
        Assert.NotNull(request.NextReconciliationAt);
    }

    private static ChargeRequest CreatePaidRequest()
    {
        var now = DateTime.UtcNow;
        var request = ChargeRequest.Create(Guid.NewGuid(), "Eniac", ChargeOperator.Irancell,
            ChargeServiceType.PinCharge, "09350000000", "09000000000", "IrancellCharge_50000",
            "شارژ ایرانسل", 1003, 0, 1005, 1, "{}", 50_000,
            null, 0, 0, 0, 50_000, Guid.NewGuid().ToString("N"), now, now.AddMinutes(20));
        var orderId = Guid.NewGuid();
        request.ConvertToOrder(orderId, now);
        request.MarkPaid(orderId, Guid.NewGuid(), now);
        return request;
    }

    private sealed class FakeChargeRequestRepository(ChargeRequest request) : IChargeRequestRepository
    {
        public int SaveCount { get; private set; }
        public Task<ChargeRequest?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(request);
        public Task<ChargeRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(request);
        public Task<ChargeRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(request);
        public Task<ChargeRequest?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(request);
        public Task<IReadOnlyList<ChargeRequest>> GetWorkItemsAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([request]);
        public Task<IReadOnlyList<ChargeRequest>> GetExpiredCandidatesAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([]);
        public Task AddAsync(ChargeRequest value, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddFulfillmentAttemptAsync(ChargeFulfillmentAttempt attempt, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class ThrowingProviderResolver : IChargeProviderResolver
    {
        public IChargeProvider Get(string providerName) => throw new InvalidOperationException("provider resolution failed");
        public IChargeProvider GetDefault() => Get("Eniac");
    }

    private sealed class CancellingProviderResolver(CancellationTokenSource cancellation) : IChargeProviderResolver
    {
        public IChargeProvider Get(string providerName)
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }

        public IChargeProvider GetDefault() => Get("Eniac");
    }

    private sealed class PassthroughProtector : IChargeSecretProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }
}
