using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public sealed class InitiatePaymentCommandHandlerTests
{
    [Fact]
    public async Task ConcurrentSameKey_CreatesOneSessionAndOneProviderPurchase()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider();
        var handler = new InitiatePaymentCommandHandler(repository,
            new FakeFactory(provider), new FakeWallets(userId, walletId, WalletTypeValues.User),
            NullLogger<InitiatePaymentCommandHandler>.Instance);
        var command = Command(userId, walletId, "same-key", 1000);

        var results = await Task.WhenAll(
            handler.Handle(command, default), handler.Handle(command, default));

        Assert.Equal(results[0], results[1]);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Single(repository.Sessions);
    }

    [Fact]
    public async Task SameKeyDifferentAmount_IsConflict()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider();
        var handler = new InitiatePaymentCommandHandler(repository,
            new FakeFactory(provider), new FakeWallets(userId, walletId, WalletTypeValues.User),
            NullLogger<InitiatePaymentCommandHandler>.Instance);
        await handler.Handle(Command(userId, walletId, "same-key", 1000), default);

        await Assert.ThrowsAsync<FundingIdempotencyConflictException>(() =>
            handler.Handle(Command(userId, walletId, "same-key", 2000), default));
    }

    [Fact]
    public async Task SameKeySameRequest_ReturnsSameSessionWithoutSecondProviderPurchase()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider();
        var handler = Handler(repository, provider, userId, walletId);
        var command = Command(userId, walletId, "same-key", 1000);

        var first = await handler.Handle(command, default);
        var second = await handler.Handle(command, default);

        Assert.Equal(first, second);
        Assert.Equal(1, provider.CreateCalls);
    }

    [Fact]
    public async Task SameKeyDifferentWallet_IsConflict()
    {
        var userId = Guid.NewGuid();
        var firstWallet = Guid.NewGuid();
        var secondWallet = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider();
        await Handler(repository, provider, userId, firstWallet)
            .Handle(Command(userId, firstWallet, "same-key", 1000), default);

        await Assert.ThrowsAsync<FundingIdempotencyConflictException>(() =>
            Handler(repository, provider, userId, secondWallet)
                .Handle(Command(userId, secondWallet, "same-key", 1000), default));
    }

    [Fact]
    public async Task SameKeyDifferentProvider_IsConflict()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider();
        var handler = Handler(repository, provider, userId, walletId);
        await handler.Handle(Command(userId, walletId, "same-key", 1000), default);

        var changed = Command(userId, walletId, "same-key", 1000) with
        {
            Provider = PaymentGatewayProviderType.Sep
        };
        await Assert.ThrowsAsync<FundingIdempotencyConflictException>(() =>
            handler.Handle(changed, default));
    }

    [Fact]
    public async Task OrgCreditWallet_IsRejectedBeforeProviderCreate()
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var provider = new FakeProvider();
        var handler = new InitiatePaymentCommandHandler(new FakeRepository(),
            new FakeFactory(provider), new FakeWallets(userId, walletId, WalletTypeValues.OrgCredit),
            NullLogger<InitiatePaymentCommandHandler>.Instance);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            handler.Handle(Command(userId, walletId, "key", 1000), default));
        Assert.Equal(0, provider.CreateCalls);
    }

    private static InitiatePaymentCommand Command(Guid userId, Guid walletId, string key, long amount) =>
        new(userId, walletId, amount, "IRR", PaymentGatewayProviderType.Jibit,
            "https://api.test/callback", "/result", IdempotencyKey: key);

    private static InitiatePaymentCommandHandler Handler(FakeRepository repository,
        FakeProvider provider, Guid userId, Guid walletId) =>
        new(repository, new FakeFactory(provider),
            new FakeWallets(userId, walletId, WalletTypeValues.User),
            NullLogger<InitiatePaymentCommandHandler>.Instance);

    private sealed class FakeRepository : IPaymentGatewaySessionRepository
    {
        private readonly object _gate = new();
        public List<PaymentGatewaySession> Sessions { get; } = [];
        public Task<PaymentGatewaySession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Sessions.SingleOrDefault(x => x.Id == id));
        public Task<PaymentGatewaySession?> GetByInitiationKeyAsync(Guid userId, string key,
            CancellationToken ct = default) => Task.FromResult(Sessions.SingleOrDefault(x =>
                x.UserId == userId && x.InitiationIdempotencyKey == key));
        public Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(Guid userId, int take,
            PaymentSessionStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentGatewaySession>>(Sessions);
        public Task<bool> TryAddAsync(PaymentGatewaySession session, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (Sessions.Any(x => x.UserId == session.UserId &&
                    x.InitiationIdempotencyKey == session.InitiationIdempotencyKey))
                    return Task.FromResult(false);
                Sessions.Add(session);
                return Task.FromResult(true);
            }
        }
        public Task UpdateAsync(PaymentGatewaySession session, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<bool> TryClaimAsync(Guid id, IReadOnlyCollection<PaymentSessionStatus> statuses,
            DateTimeOffset now, DateTimeOffset lease, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Guid>> GetRecoveryCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> GetReversalCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> TryClaimReversalAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeProvider : IPaymentGatewayProvider
    {
        public int CreateCalls;
        public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Jibit;
        public async Task<GetTokenResult> GetTokenAsync(GetTokenRequest request,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref CreateCalls);
            await Task.Delay(100, ct);
            return new GetTokenResult(true, "https://switch", "purchase", "purchase");
        }
        public string BuildRedirectUrl(string token) => token;
        public Task<VerifyResult> VerifyAsync(VerifyRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeFactory(FakeProvider provider) : IPaymentGatewayProviderFactory
    {
        public IPaymentGatewayProvider GetProvider(PaymentGatewayProviderType type) => provider;
    }

    private sealed class FakeWallets(Guid ownerId, Guid walletId, short walletType)
        : IWalletReadRepository
    {
        public Task<WalletInfoDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<WalletInfoDto?>(id == walletId
                ? new WalletInfoDto(walletId, ownerId, walletType, 1, "IRR", null, null)
                : null);
        public Task<List<WalletSummaryDto>> GetByOwnerIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<bool> ExistsByOwnerAndTypeAsync(Guid id, short type, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<WalletBalanceResponse> GetWalletBalanceAsync(Guid id) => throw new NotSupportedException();
        public Task<IReadOnlyList<GetTransactionsResponse>> GetWalletTransactionsAsync(Guid id, int take) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<MyWalletTransactionDto>> GetOwnerWalletTransactionsAsync(
            Guid id, int take, string? type = null, short? operation = null,
            short? entry = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
