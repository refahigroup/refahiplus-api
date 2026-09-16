using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.Recovery;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Application.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Features.Recovery;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public sealed class ProcessCallbackCommandHandlerTests
{
    [Fact]
    public async Task VerifiedBoundTransaction_CreditsWalletOnce()
    {
        var session = CreateSession();
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var result = await handler.Handle(CreateCommand(session), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Succeeded, session.Status);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task ProviderMismatch_IsRejectedBeforeVerifyOrWalletMutation()
    {
        var session = CreateSession();
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var command = CreateCommand(session) with { Provider = PaymentGatewayProviderType.Jibit };
        var result = await handler.Handle(command, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, provider.VerifyCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task CallbackAttemptMismatch_IsRejectedBeforeVerify()
    {
        var session = CreateSession();
        var provider = SuccessProvider(session);
        var handler = CreateHandler(session, provider,
            new FakeMediator(CommandStatus.Completed));

        var result = await handler.Handle(CreateCommand(session) with { Token = "other" }, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, provider.VerifyCalls);
    }

    [Fact]
    public async Task AuthoritativeAmountMismatch_DoesNotCreditAndReversesDurably()
    {
        var session = CreateSession();
        var provider = new FakeProvider(new VerifyResult(
            FundingProviderOutcome.VerifiedSuccess, "txn-1", 900, "IRR",
            session.Id.ToString(), 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var result = await handler.Handle(CreateCommand(session), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, mediator.TopUpCalls);
        Assert.Equal(1, provider.ReverseCalls);
        Assert.Equal(FundingReversalStatus.Succeeded, session.ReversalStatus);
    }

    [Fact]
    public async Task WalletInProgress_MovesToPendingRecovery_WithoutReverse()
    {
        var session = CreateSession();
        var provider = SuccessProvider(session);
        var handler = CreateHandler(session, provider,
            new FakeMediator(CommandStatus.InProgress));

        await handler.Handle(CreateCommand(session), default);

        Assert.Equal(PaymentSessionStatus.PendingRecovery, session.Status);
        Assert.Equal(0, provider.ReverseCalls);
    }

    [Fact]
    public async Task WalletInProgress_ThenRecoveryCompleted_SucceedsWithoutReverse()
    {
        var session = CreateSession();
        var repository = new FakeSessionRepository(session);
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.InProgress);
        var handler = CreateHandler(repository, provider, mediator);
        await handler.Handle(CreateCommand(session), default);
        mediator.Status = CommandStatus.Completed;

        await handler.Handle(CreateCommand(session) with { IsRecovery = true }, default);

        Assert.Equal(PaymentSessionStatus.Succeeded, session.Status);
        Assert.Equal(2, mediator.TopUpCalls);
        Assert.Equal(0, provider.ReverseCalls);
    }

    [Fact]
    public async Task ProviderTimeoutOutcome_MovesToPendingRecovery()
    {
        var session = CreateSession();
        var provider = new FakeProvider(new VerifyResult(
            FundingProviderOutcome.Unknown, "txn-1", null, null, null, -99, "timeout"));
        var handler = CreateHandler(session, provider,
            new FakeMediator(CommandStatus.Completed));

        await handler.Handle(CreateCommand(session), default);

        Assert.Equal(PaymentSessionStatus.PendingRecovery, session.Status);
        Assert.Equal(0, provider.ReverseCalls);
    }

    [Fact]
    public async Task ConcurrentCallbacks_OnlyClaimOwnerCanVerifyAndCredit()
    {
        var session = CreateSession();
        var repo = new FakeSessionRepository(session);
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(repo, provider, mediator);

        await Task.WhenAll(
            handler.Handle(CreateCommand(session), default),
            handler.Handle(CreateCommand(session), default));

        Assert.Equal(1, provider.VerifyCalls);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task CallbackAndRecovery_CompeteForOneFinancialOwner()
    {
        var session = CreateSession();
        var repository = new FakeSessionRepository(session);
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(repository, provider, mediator);

        await Task.WhenAll(
            handler.Handle(CreateCommand(session), default),
            handler.Handle(CreateCommand(session) with { IsRecovery = true }, default));

        Assert.Equal(1, provider.VerifyCalls + provider.InquiryCalls);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task TwoRecoveryWorkers_OnlyOneInquiresAndCredits()
    {
        var session = CreateSession();
        session.MarkPendingRecovery(null, "timeout", DateTimeOffset.UtcNow);
        var repository = new FakeSessionRepository(session);
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(repository, provider, mediator);
        var recovery = CreateCommand(session) with { IsRecovery = true };

        await Task.WhenAll(handler.Handle(recovery, default), handler.Handle(recovery, default));

        Assert.Equal(1, provider.InquiryCalls);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task ReplayedCallback_AfterSuccess_DoesNotVerifyOrCreditAgain()
    {
        var session = CreateSession();
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        await handler.Handle(CreateCommand(session), default);
        await handler.Handle(CreateCommand(session), default);

        Assert.Equal(1, provider.VerifyCalls);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task ReverseFailure_RemainsDurableAndRetryable()
    {
        var session = CreateSession();
        var provider = new FakeProvider(new VerifyResult(
            FundingProviderOutcome.VerifiedSuccess, "txn-1", 900, "IRR",
            session.Id.ToString(), 0), new ReverseResult(false, -1, "reverse timeout"));
        var handler = CreateHandler(session, provider,
            new FakeMediator(CommandStatus.Completed));

        await handler.Handle(CreateCommand(session), default);

        Assert.Equal(FundingReversalStatus.Failed, session.ReversalStatus);
        Assert.NotNull(session.NextReversalAt);
        Assert.Equal(1, session.ReversalAttemptCount);
    }

    [Fact]
    public async Task FailedReverse_CanBeRetriedToDurableSuccess()
    {
        var session = CreateSession();
        var repository = new FakeSessionRepository(session);
        var provider = new FakeProvider(new VerifyResult(
            FundingProviderOutcome.VerifiedSuccess, "txn-1", 900, "IRR",
            session.Id.ToString(), 0), new ReverseResult(false, -1, "timeout"));
        await CreateHandler(repository, provider, new FakeMediator(CommandStatus.Completed))
            .Handle(CreateCommand(session), default);
        provider.ReverseOutcome = new ReverseResult(true, 0);
        var retry = new RetryFundingReversalCommandHandler(repository,
            new FakeFactory(provider),
            NullLogger<RetryFundingReversalCommandHandler>.Instance);

        await retry.Handle(new RetryFundingReversalCommand(session.Id), default);

        Assert.Equal(FundingReversalStatus.Succeeded, session.ReversalStatus);
        Assert.Equal(2, session.ReversalAttemptCount);
    }

    [Fact]
    public async Task FinalDatabaseWriteFailure_DoesNotTriggerReverseOrDefinitiveFailure()
    {
        var session = CreateSession();
        var repository = new FakeSessionRepository(session, failSucceededUpdate: true);
        var provider = SuccessProvider(session);
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(repository, provider, mediator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(CreateCommand(session), default));

        Assert.Equal(1, mediator.TopUpCalls);
        Assert.Equal(0, provider.ReverseCalls);
        Assert.NotEqual(PaymentSessionStatus.Failed, session.Status);
    }

    [Fact]
    public async Task NonReversibleProvider_InconsistencyStaysOperationallyVisible()
    {
        var session = PaymentGatewaySession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000, "IRR",
            PaymentGatewayProviderType.Jibit, "/result");
        session.MarkAsTokenReceived("https://switch", "purchase-1", "purchase-1");
        var provider = new NonReversibleProvider(new VerifyResult(
            FundingProviderOutcome.VerifiedSuccess, "purchase-1", 900, "IRR",
            session.Id.ToString(), 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(new FakeSessionRepository(session), provider, mediator);
        var command = new ProcessCallbackCommand(PaymentGatewayProviderType.Jibit, "OK",
            "purchase-1", session.Id.ToString(), null, null, "{}");

        await handler.Handle(command, default);

        Assert.Equal(PaymentSessionStatus.PendingRecovery, session.Status);
        Assert.Equal(FundingReversalStatus.Unsupported, session.ReversalStatus);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    private static PaymentGatewaySession CreateSession()
    {
        var session = PaymentGatewaySession.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000, "IRR",
            PaymentGatewayProviderType.Sep, "/result");
        session.MarkAsTokenReceived("sep-token", "sep-token");
        return session;
    }

    private static ProcessCallbackCommand CreateCommand(PaymentGatewaySession session) =>
        new(session.Provider, "OK", "txn-1", session.Id.ToString(), null, null,
            "{}", Token: "sep-token");

    private static FakeProvider SuccessProvider(PaymentGatewaySession session) =>
        new(new VerifyResult(FundingProviderOutcome.VerifiedSuccess, "txn-1", 1000,
            "IRR", session.Id.ToString(), 0));

    private static ProcessCallbackCommandHandler CreateHandler(
        PaymentGatewaySession session, IPaymentGatewayProvider provider, FakeMediator mediator) =>
        CreateHandler(new FakeSessionRepository(session), provider, mediator);

    private static ProcessCallbackCommandHandler CreateHandler(
        FakeSessionRepository repository, IPaymentGatewayProvider provider, FakeMediator mediator) =>
        new(repository, new FakeFactory(provider), mediator,
            Options.Create(new FundingSafetyOptions { RecoveryBaseDelaySeconds = 1 }),
            NullLogger<ProcessCallbackCommandHandler>.Instance);

    private sealed class FakeSessionRepository(PaymentGatewaySession session,
        bool failSucceededUpdate = false)
        : IPaymentGatewaySessionRepository
    {
        private int _claimed;
        public Task<PaymentGatewaySession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<PaymentGatewaySession?>(id == session.Id ? session : null);
        public Task<PaymentGatewaySession?> GetByInitiationKeyAsync(Guid userId, string key,
            CancellationToken ct = default) => Task.FromResult<PaymentGatewaySession?>(null);
        public Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(Guid userId, int take,
            PaymentSessionStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentGatewaySession>>([session]);
        public Task<bool> TryAddAsync(PaymentGatewaySession value, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> TryClaimAsync(Guid id, IReadOnlyCollection<PaymentSessionStatus> statuses,
            DateTimeOffset now, DateTimeOffset lease, CancellationToken ct = default) =>
            Task.FromResult(Interlocked.CompareExchange(ref _claimed, 1, 0) == 0);
        public Task<IReadOnlyList<Guid>> GetRecoveryCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> GetReversalCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> TryClaimReversalAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task UpdateAsync(PaymentGatewaySession value, CancellationToken ct = default)
        {
            if (failSucceededUpdate && value.Status == PaymentSessionStatus.Succeeded)
                throw new InvalidOperationException("simulated database outage");
            if (value.Status == PaymentSessionStatus.PendingRecovery)
                Interlocked.Exchange(ref _claimed, 0);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFactory(IPaymentGatewayProvider provider) : IPaymentGatewayProviderFactory
    {
        public IPaymentGatewayProvider GetProvider(PaymentGatewayProviderType type) => provider;
    }

    private sealed class NonReversibleProvider(VerifyResult result) : IPaymentGatewayProvider
    {
        public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Jibit;
        public Task<GetTokenResult> GetTokenAsync(GetTokenRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();
        public string BuildRedirectUrl(string token) => token;
        public Task<VerifyResult> VerifyAsync(VerifyRequest request,
            CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class FakeProvider(VerifyResult result, ReverseResult? reverseResult = null)
        : IReversiblePaymentGatewayProvider, IRecoverablePaymentGatewayProvider
    {
        public int VerifyCalls;
        public int InquiryCalls;
        public int ReverseCalls;
        public ReverseResult ReverseOutcome = reverseResult ?? new ReverseResult(true, 0);
        public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Sep;
        public Task<GetTokenResult> GetTokenAsync(GetTokenRequest request,
            CancellationToken ct = default) => Task.FromResult(
                new GetTokenResult(true, "token", "token"));
        public string BuildRedirectUrl(string token) => token;
        public Task<VerifyResult> VerifyAsync(VerifyRequest request,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref VerifyCalls);
            return Task.FromResult(result);
        }
        public Task<VerifyResult> InquireAsync(FundingInquiryRequest request,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref InquiryCalls);
            return Task.FromResult(result);
        }
        public Task<ReverseResult> ReverseAsync(ReverseRequest request,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref ReverseCalls);
            return Task.FromResult(ReverseOutcome);
        }
    }

    private sealed class FakeMediator(CommandStatus status) : IMediator
    {
        public int TopUpCalls;
        public CommandStatus Status { get; set; } = status;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,
            CancellationToken ct = default)
        {
            if (request is not TopUpWalletCommand topUp)
                throw new NotSupportedException();
            Interlocked.Increment(ref TopUpCalls);
            var data = Status == CommandStatus.Completed
                ? new TopUpWalletResponse(topUp.WalletId, Guid.NewGuid(), Guid.NewGuid(),
                    topUp.AmountMinor, topUp.Currency, topUp.AmountMinor, DateTimeOffset.UtcNow)
                : null;
            return Task.FromResult((TResponse)(object)new CommandResponse<TopUpWalletResponse>(Status, data));
        }
        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
