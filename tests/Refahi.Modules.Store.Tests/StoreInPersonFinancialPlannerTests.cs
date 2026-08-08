using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class StoreInPersonFinancialPlannerTests
{
    private static readonly Guid SupplierId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProviderWalletId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RevenueWalletId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid VatWalletId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task BuildAsync_DoesNotRequireSystemWallets_WhenCommissionIsZero()
    {
        var mediator = new FinancialPlannerMediator();
        var planner = CreatePlanner(mediator);

        var result = await planner.BuildAsync(SupplierId, 1_000_000, 0m, true, CancellationToken.None);

        Assert.Equal(1_000_000, result.VendorNetAmountMinor);
        Assert.Equal(0, result.CommissionAmountMinor);
        Assert.Equal(0, result.VatAmountMinor);
        Assert.Single(result.Postings);
        Assert.Empty(mediator.RequestedSystemWalletIds);
    }

    [Fact]
    public async Task BuildAsync_DoesNotRequireVatWallet_WhenVatIsNotApplicable()
    {
        var mediator = new FinancialPlannerMediator();
        var planner = CreatePlanner(mediator);

        var result = await planner.BuildAsync(SupplierId, 1_000_000, 10m, false, CancellationToken.None);

        Assert.Equal(100_000, result.CommissionAmountMinor);
        Assert.Equal(0, result.VatAmountMinor);
        Assert.Equal(900_000, result.VendorNetAmountMinor);
        Assert.Equal([RevenueWalletId], mediator.RequestedSystemWalletIds);
        Assert.Equal(3, result.Postings.Count);
    }

    [Fact]
    public async Task BuildAsync_UsesBothConfiguredSystemWallets_WhenCommissionAndVatApply()
    {
        var mediator = new FinancialPlannerMediator();
        var planner = CreatePlanner(mediator);

        var result = await planner.BuildAsync(SupplierId, 1_000_000, 10m, true, CancellationToken.None);

        Assert.Equal(100_000, result.CommissionAmountMinor);
        Assert.Equal(10_000, result.VatAmountMinor);
        Assert.Equal(890_000, result.VendorNetAmountMinor);
        Assert.Equal([RevenueWalletId, VatWalletId], mediator.RequestedSystemWalletIds);
        Assert.Equal(5, result.Postings.Count);
    }

    private static StoreInPersonFinancialPlanner CreatePlanner(IMediator mediator) => new(
        mediator,
        Options.Create(new StorePaymentDistributionOptions
        {
            RefahiRevenueWalletId = RevenueWalletId,
            RefahiVatWalletId = VatWalletId,
            VatRatePercent = 10m
        }));

    private sealed class FinancialPlannerMediator : IMediator
    {
        public List<Guid> RequestedSystemWalletIds { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetMyWalletsQuery => new List<WalletSummaryDto>
                {
                    new(ProviderWalletId, "Provider", "IRR", 0, 0, 0)
                },
                GetWalletInfoByIdQuery query => GetSystemWallet(query.WalletId),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };

            return Task.FromResult((TResponse)response);
        }

        private WalletInfoDto GetSystemWallet(Guid walletId)
        {
            RequestedSystemWalletIds.Add(walletId);
            return new WalletInfoDto(walletId, 1, 1, "IRR", null, null);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
