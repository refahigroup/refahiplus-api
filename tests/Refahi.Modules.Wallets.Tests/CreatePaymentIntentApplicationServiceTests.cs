using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Application.Services;
using Refahi.Modules.Wallets.Domain.Enums;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class CreatePaymentIntentApplicationServiceTests
{
    [Fact]
    public async Task Reserve_RejectsWalletOwnedByAnotherPayer()
    {
        var payerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var service = CreateService(
            new WalletInfoDto(walletId, Guid.NewGuid(), (short)WalletType.User, 1, "IRR", null, null),
            out var writer
        );

        await Assert.ThrowsAsync<WalletOperationNotAllowedException>(() =>
            service.CreateIntentAsync(CreateCommand(walletId, payerId, ["store"]), default));
        Assert.False(writer.WasCalled);
    }

    [Fact]
    public async Task RestrictedCredit_FailsClosedWithoutCompleteCategoryContext()
    {
        var payerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var service = CreateService(
            new WalletInfoDto(walletId, payerId, (short)WalletType.OrgCredit, 1, "IRR", "store", null),
            out var writer
        );

        await Assert.ThrowsAsync<WalletOperationNotAllowedException>(() =>
            service.CreateIntentAsync(CreateCommand(walletId, payerId, [null]), default));
        Assert.False(writer.WasCalled);
    }

    [Fact]
    public async Task RestrictedCredit_RejectsExpiredContract()
    {
        var payerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var service = CreateService(
            new WalletInfoDto(
                walletId,
                payerId,
                (short)WalletType.OrgCredit,
                1,
                "IRR",
                "store",
                DateTimeOffset.UtcNow.AddSeconds(-1)),
            out var writer
        );

        await Assert.ThrowsAsync<WalletOperationNotAllowedException>(() =>
            service.CreateIntentAsync(CreateCommand(walletId, payerId, ["store"]), default));
        Assert.False(writer.WasCalled);
    }

    [Theory]
    [InlineData("store")]
    [InlineData("store.clothing")]
    public async Task RestrictedCredit_AllowsExactOrChildCategory(string category)
    {
        var payerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var service = CreateService(
            new WalletInfoDto(walletId, payerId, (short)WalletType.OrgCredit, 1, "IRR", "store", null),
            out var writer
        );

        await service.CreateIntentAsync(CreateCommand(walletId, payerId, [category]), default);
        Assert.True(writer.WasCalled);
    }

    private static CreatePaymentIntentApplicationService CreateService(
        WalletInfoDto wallet,
        out StubAtomicWriter writer
    )
    {
        writer = new StubAtomicWriter();
        return new CreatePaymentIntentApplicationService(
            writer,
            new StubWalletRepository(wallet),
            NullLogger<CreatePaymentIntentApplicationService>.Instance
        );
    }

    private static CreatePaymentIntentCommand CreateCommand(
        Guid walletId,
        Guid payerId,
        IReadOnlyList<string?> categories
    ) => new(
        Guid.NewGuid(),
        100,
        "IRR",
        [new AllocationRequest(walletId, 100)],
        "idem",
        OrderItemCategoryCode: categories,
        PayerOwnerId: payerId
    );

    private sealed class StubAtomicWriter : IPaymentAtomicWriter
    {
        public bool WasCalled { get; private set; }

        public Task<CreateIntentAtomicResult> ExecuteCreateIntentAsync(
            Guid orderId, string idempotencyKey, long totalAmountMinor, string currency,
            List<AllocationInput> allocations, string? metadataJson, Guid? destinationWalletId,
            CancellationToken ct, IReadOnlyList<PaymentPostingInput>? postings = null,
            Guid? payerOwnerId = null, IReadOnlyList<string?>? categoryCodes = null)
        {
            WasCalled = true;
            return Task.FromResult(new CreateIntentAtomicResult(
                CreateIntentOutcome.Created, Guid.NewGuid(), orderId, totalAmountMinor, currency,
                allocations.Select(x => new AllocationOutput(x.WalletId, x.AmountMinor)).ToList(),
                DateTimeOffset.UtcNow));
        }

        public Task<CaptureIntentAtomicResult> ExecuteCaptureIntentAsync(Guid intentId, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReleaseIntentAtomicResult> ExecuteReleaseIntentAsync(Guid intentId, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<RefundPaymentAtomicResult> ExecuteRefundPaymentAsync(Guid paymentId, string key, string? reason, string? metadata, CancellationToken ct, long? amountMinor = null) => throw new NotSupportedException();
    }

    private sealed class StubWalletRepository(WalletInfoDto wallet) : IWalletReadRepository
    {
        public Task<WalletInfoDto?> GetByIdAsync(Guid walletId, CancellationToken ct = default) =>
            Task.FromResult<WalletInfoDto?>(walletId == wallet.WalletId ? wallet : null);
        public Task<WalletBalanceResponse> GetWalletBalanceAsync(Guid walletId) => throw new NotSupportedException();
        public Task<IReadOnlyList<GetTransactionsResponse>> GetWalletTransactionsAsync(Guid walletId, int take) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyWalletTransactionDto>> GetOwnerWalletTransactionsAsync(Guid ownerId, int take, string? walletType = null, short? operationType = null, short? entryType = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<WalletSummaryDto>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByOwnerAndTypeAsync(Guid ownerId, short walletType, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
