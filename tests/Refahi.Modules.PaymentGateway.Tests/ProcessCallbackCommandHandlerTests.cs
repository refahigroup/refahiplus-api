using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Application.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public class ProcessCallbackCommandHandlerTests
{
    [Fact]
    public async Task Handle_SuccessfulSepCallback_VerifiesAndTopsUpWallet()
    {
        var session = CreateSession(amount: 1000);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(CreateCommand(session.Id, amount: 1000), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Succeeded, session.Status);
        Assert.Equal(1, provider.VerifyCalls);
        Assert.Equal(1, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_EmptyRefNum_FailsWithoutVerifyOrTopUp()
    {
        var session = CreateSession(amount: 1000);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(CreateCommand(session.Id, refNum: null, amount: 1000), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Failed, session.Status);
        Assert.Equal(0, provider.VerifyCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_InvalidResNum_ReturnsInvalidResultWithoutRepositoryLookup()
    {
        var repository = new FakeSessionRepository(null);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = new ProcessCallbackCommandHandler(
            repository,
            new FakeProviderFactory(provider),
            mediator,
            new TestLogger<ProcessCallbackCommandHandler>());

        var response = await handler.Handle(
            new ProcessCallbackCommand(
                Provider: PaymentGatewayProviderType.Sep,
                State: "OK",
                RefNum: "ref-1",
                ResNum: "not-a-guid",
                TraceNo: null,
                SecurePan: null,
                RawCallbackJson: "{}"),
            CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(Guid.Empty, response.SessionId);
        Assert.Equal(0, repository.GetByIdCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_NonOkState_FailsWithoutVerifyOrTopUp()
    {
        var session = CreateSession(amount: 1000);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(CreateCommand(session.Id, state: "CanceledByUser", amount: 1000), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Failed, session.Status);
        Assert.Equal(0, provider.VerifyCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_InvalidCallbackAmount_FailsWithoutVerifyOrTopUp()
    {
        var session = CreateSession(amount: 1000);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(
            CreateCommand(session.Id, amount: null, amountParseFailed: true),
            CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Failed, session.Status);
        Assert.Equal(0, provider.VerifyCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_TerminalSession_DoesNotTopUpAgain()
    {
        var session = CreateSession(amount: 1000);
        session.MarkAsSucceeded(Guid.NewGuid(), 0);
        var provider = new FakeProvider(new VerifyResult(true, 1000, 0));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(CreateCommand(session.Id, amount: 1000), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Succeeded, session.Status);
        Assert.Equal(0, provider.VerifyCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    [Fact]
    public async Task Handle_VerifiedAmountMismatch_ReversesSepTransaction()
    {
        var session = CreateSession(amount: 1000);
        var provider = new FakeProvider(new VerifyResult(false, 900, 0, "amount mismatch"));
        var mediator = new FakeMediator(CommandStatus.Completed);
        var handler = CreateHandler(session, provider, mediator);

        var response = await handler.Handle(CreateCommand(session.Id, amount: 1000), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Failed, session.Status);
        Assert.Equal(1, provider.VerifyCalls);
        Assert.Equal(1, provider.ReverseCalls);
        Assert.Equal(0, mediator.TopUpCalls);
    }

    private static ProcessCallbackCommandHandler CreateHandler(
        PaymentGatewaySession session,
        FakeProvider provider,
        FakeMediator mediator)
    {
        return new ProcessCallbackCommandHandler(
            new FakeSessionRepository(session),
            new FakeProviderFactory(provider),
            mediator,
            new TestLogger<ProcessCallbackCommandHandler>());
    }

    private static PaymentGatewaySession CreateSession(long amount)
    {
        return PaymentGatewaySession.Create(
            sessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            walletId: Guid.NewGuid(),
            amountMinor: amount,
            currency: "IRR",
            provider: PaymentGatewayProviderType.Sep,
            returnBaseUrl: "/charge/wallet/topup/result");
    }

    private static ProcessCallbackCommand CreateCommand(
        Guid sessionId,
        string state = "OK",
        string? refNum = "ref-1",
        long? amount = null,
        bool amountParseFailed = false)
    {
        return new ProcessCallbackCommand(
            Provider: PaymentGatewayProviderType.Sep,
            State: state,
            RefNum: refNum,
            ResNum: sessionId.ToString(),
            TraceNo: "trace-1",
            SecurePan: "621986****8080",
            RawCallbackJson: "{}",
            AmountMinor: amount,
            AmountParseFailed: amountParseFailed);
    }

    private sealed class FakeSessionRepository : IPaymentGatewaySessionRepository
    {
        private readonly PaymentGatewaySession? _session;
        public int GetByIdCalls { get; private set; }

        public FakeSessionRepository(PaymentGatewaySession? session)
        {
            _session = session;
        }

        public Task<PaymentGatewaySession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
        {
            GetByIdCalls++;
            return Task.FromResult(_session?.Id == sessionId ? _session : null);
        }

        public Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(
            Guid userId,
            int take,
            PaymentSessionStatus? status = null,
            CancellationToken ct = default)
        {
            IReadOnlyList<PaymentGatewaySession> sessions =
                _session is not null
                && _session.UserId == userId
                && (!status.HasValue || _session.Status == status.Value)
                    ? [_session]
                    : [];

            return Task.FromResult(sessions);
        }

        public Task AddAsync(PaymentGatewaySession session, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProviderFactory : IPaymentGatewayProviderFactory
    {
        private readonly IPaymentGatewayProvider _provider;

        public FakeProviderFactory(IPaymentGatewayProvider provider)
        {
            _provider = provider;
        }

        public IPaymentGatewayProvider GetProvider(PaymentGatewayProviderType providerType) => _provider;
    }

    private sealed class FakeProvider : IReversiblePaymentGatewayProvider
    {
        private readonly VerifyResult _verifyResult;

        public int VerifyCalls { get; private set; }
        public int ReverseCalls { get; private set; }
        public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Sep;

        public FakeProvider(VerifyResult verifyResult)
        {
            _verifyResult = verifyResult;
        }

        public Task<GetTokenResult> GetTokenAsync(GetTokenRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new GetTokenResult(true, "token"));
        }

        public string BuildRedirectUrl(string token) => token;

        public Task<VerifyResult> VerifyAsync(VerifyRequest request, CancellationToken ct = default)
        {
            VerifyCalls++;
            return Task.FromResult(_verifyResult);
        }

        public Task<ReverseResult> ReverseAsync(ReverseRequest request, CancellationToken ct = default)
        {
            ReverseCalls++;
            return Task.FromResult(new ReverseResult(true, 0));
        }
    }

    private sealed class FakeMediator : IMediator
    {
        private readonly CommandStatus _topUpStatus;
        private readonly Exception? _exception;
        public int TopUpCalls { get; private set; }

        public FakeMediator(CommandStatus topUpStatus, Exception? exception = null)
        {
            _topUpStatus = topUpStatus;
            _exception = exception;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is TopUpWalletCommand topUpCommand)
            {
                TopUpCalls++;

                if (_exception is not null)
                    throw _exception;

                var response = new CommandResponse<TopUpWalletResponse>(
                    _topUpStatus,
                    _topUpStatus == CommandStatus.Completed
                        ? new TopUpWalletResponse(
                            topUpCommand.WalletId,
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            topUpCommand.AmountMinor,
                            topUpCommand.Currency,
                            topUpCommand.AmountMinor,
                            DateTimeOffset.UtcNow)
                        : null);

                return Task.FromResult((TResponse)(object)response);
            }

            throw new InvalidOperationException($"Unexpected mediator request: {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
