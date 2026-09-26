using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class MerchantProvisioningTests
{
    private const string NationalId = "1234567890";

    [Fact] public async Task Merchant_is_created_without_supplier_with_own_wallet()
    {
        var (mediator, repo, _) = Build();
        var result = await mediator.Send(new CreateMerchantCommand(1, NationalId, "نام قانونی", null));
        Assert.Equal("نام قانونی", result.DisplayName);
        Assert.Equal(result.Id, repo.Merchants.Single().Id);
        Assert.NotNull(result.SettlementWalletId);
    }

    [Fact] public async Task Identical_request_is_idempotent_and_conflicting_identity_is_rejected()
    {
        var (mediator, repo, _) = Build();
        var command = new CreateMerchantCommand(1, NationalId, "نام قانونی", null);
        var first = await mediator.Send(command);
        var second = await mediator.Send(command);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(repo.Merchants);
        await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            mediator.Send(command with { LegalName = "نام متفاوت" }));
    }

    [Fact] public async Task Persian_digits_are_normalized()
    {
        var (mediator, _, _) = Build();
        var result = await mediator.Send(new CreateMerchantCommand(1, "۱۲۳۴۵۶۷۸۹۰", "نام", null));
        Assert.Equal(NationalId, result.NationalId);
    }

    [Fact] public async Task Failed_wallet_creation_leaves_pending_merchant_and_retry_recovers()
    {
        var (mediator, repo, wallet) = Build();
        wallet.FailNext = true;
        var command = new CreateMerchantCommand(1, NationalId, "نام", null);
        await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() => mediator.Send(command));
        var pending = Assert.Single(repo.Merchants);
        Assert.Equal(MerchantStatus.Pending, pending.Status);
        Assert.Null(pending.SettlementWalletId);
        var completed = await mediator.Send(command);
        Assert.Equal(pending.Id, completed.Id);
        Assert.Equal(wallet.WalletId, completed.SettlementWalletId);
    }

    [Fact] public async Task Activation_requires_wallet_owned_by_merchant()
    {
        var (mediator, _, wallet) = Build();
        var merchant = await mediator.Send(new CreateMerchantCommand(1, NationalId, "نام", null));
        wallet.OwnerOverride = Guid.NewGuid();
        await Assert.ThrowsAsync<CreditPaymentsDomainException>(() =>
            mediator.Send(new ChangeMerchantStatusCommand(merchant.Id, "activate")));
        wallet.OwnerOverride = null;
        var active = await mediator.Send(new ChangeMerchantStatusCommand(merchant.Id, "activate"));
        Assert.Equal((short)MerchantStatus.Active, active.Status);
    }

    [Fact] public async Task Wallet_owned_by_another_merchant_is_not_bound_during_creation()
    {
        var (mediator, repo, wallet) = Build();
        wallet.OwnerOverride = Guid.NewGuid();
        await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            mediator.Send(new CreateMerchantCommand(1, NationalId, "نام", null)));
        Assert.Null(Assert.Single(repo.Merchants).SettlementWalletId);
    }

    private static (IMediator Mediator, MemoryRepository Repository, WalletHandler Wallet) Build()
    {
        var repo = new MemoryRepository();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICreditPaymentsRepository>(repo);
        services.AddMediatR(typeof(DI).Assembly, typeof(MerchantProvisioningTests).Assembly);
        var wallet = new WalletHandler();
        services.AddSingleton<IRequestHandler<CreateWalletCommand, CreateWalletResponse>>(wallet);
        services.AddSingleton<IRequestHandler<GetWalletInfoByIdQuery, WalletInfoDto?>>(wallet);
        return (services.BuildServiceProvider().GetRequiredService<IMediator>(), repo, wallet);
    }

    public sealed class WalletHandler : IRequestHandler<CreateWalletCommand, CreateWalletResponse>, IRequestHandler<GetWalletInfoByIdQuery, WalletInfoDto?>
    {
        public bool FailNext { get; set; }
        public Guid? OwnerOverride { get; set; }
        public Guid WalletId { get; } = Guid.NewGuid();
        private Guid ownerId;
        public Task<CreateWalletResponse> Handle(CreateWalletCommand request, CancellationToken ct) =>
            FailNext ? Fail() : Success(request);
        private Task<CreateWalletResponse> Fail() { FailNext = false; throw new InvalidOperationException("Wallet unavailable"); }
        private Task<CreateWalletResponse> Success(CreateWalletCommand request) { ownerId = request.OwnerId; return Task.FromResult(new CreateWalletResponse(WalletId, WalletTypeCodes.Merchant, request.Currency)); }
        public Task<WalletInfoDto?> Handle(GetWalletInfoByIdQuery request, CancellationToken ct) =>
            Task.FromResult<WalletInfoDto?>(request.WalletId == WalletId ? new(WalletId, OwnerOverride ?? ownerId, 5, 1, "IRR", null, null) : null);
    }

    private sealed class MemoryRepository : ICreditPaymentsRepository
    {
        public List<Merchant> Merchants { get; } = [];
        public Task<Merchant?> GetMerchantAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult(Merchants.SingleOrDefault(x => x.Id == id));
        public Task<Merchant?> GetMerchantByNationalIdAsync(MerchantEntityType type, string nationalId, bool tracking, CancellationToken ct) => Task.FromResult(Merchants.SingleOrDefault(x => x.EntityType == type && x.NationalId == nationalId));
        public Task<bool> TryCreateMerchantAsync(Merchant merchant, CancellationToken ct) { Merchants.Add(merchant); return Task.FromResult(true); }
        public Task<Terminal?> GetTerminalAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult<Terminal?>(null);
        public Task<TerminalCredential?> GetCredentialAsync(Guid id, bool tracking, CancellationToken ct) => Task.FromResult<TerminalCredential?>(null);
        public Task<(TerminalCredential Credential, Terminal Terminal, Merchant Merchant)?> GetAuthenticationGraphAsync(string id, CancellationToken ct) => Task.FromResult<(TerminalCredential, Terminal, Merchant)?>(null);
        public void Add(Merchant x) => Merchants.Add(x);
        public void Add(Terminal x) { }
        public void Add(TerminalReturnUrl x) { }
        public void Add(TerminalCredential x) { }
        public void Add(WebhookEndpoint x) { }
        public void AddAudit(AuditRecord x) { }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<bool> TrySaveManagementChangesAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<bool> TrySaveTerminalCreationAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
