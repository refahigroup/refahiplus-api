using Microsoft.Extensions.Configuration;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Application.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeQuoteFlowTests
{
    [Fact]
    public async Task Preview_calculates_price_without_creating_request()
    {
        var handler = new PreviewChargeRequestHandler(CreateQuoteService());

        var quote = await handler.Handle(new PreviewChargeRequestCommand(
            ChargeOperator.Rightel,
            ChargeServiceType.DirectCharge,
            "09210000000",
            null,
            50_000,
            null,
            1), CancellationToken.None);

        Assert.Equal(50_000, quote.ProviderCostMinor);
        Assert.Equal(50_000, quote.FinalAmountMinor);
        Assert.Equal("IRR", quote.Currency);
    }

    [Fact]
    public async Task Changed_price_does_not_persist_request()
    {
        var repository = new FakeChargeRequestRepository();
        var handler = new CreateChargeRequestHandler(repository, CreateQuoteService());

        var exception = await Assert.ThrowsAsync<ChargeQuoteChangedException>(() =>
            handler.Handle(new CreateChargeRequestCommand(
                Guid.NewGuid(),
                ChargeOperator.Rightel,
                ChargeServiceType.DirectCharge,
                "09210000000",
                null,
                null,
                50_000,
                null,
                1,
                40_000,
                "test-key"), CancellationToken.None));

        Assert.Equal(50_000, exception.Quote.FinalAmountMinor);
        Assert.Equal(0, repository.AddCount);
    }

    private static ChargeRequestQuoteService CreateQuoteService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Charge:RequestTtlMinutes"] = "20"
            })
            .Build();
        var pricing = new ChargePricingService(new EmptyMarkupRuleRepository());
        return new ChargeRequestQuoteService(
            new FakeProviderResolver(new FakeChargeProvider()),
            pricing,
            configuration);
    }

    private sealed class FakeProviderResolver(IChargeProvider provider) : IChargeProviderResolver
    {
        public IChargeProvider Get(string providerName) => provider;
        public IChargeProvider GetDefault() => provider;
    }

    private sealed class FakeChargeProvider : IChargeProvider
    {
        public string Name => "Test";
        public Task<IReadOnlyList<ChargeProductDto>> GetProductsAsync(ChargeOperator @operator, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeProductDto>> GetOffersAsync(ChargeOperator @operator, string mobileNumber, ChargeOfferCategory category, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargeEligibilityDto> CheckEligibilityAsync(ChargeEligibilityRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargePostpaidBalanceDto> GetPostpaidBalanceAsync(ChargeOperator @operator, string mobileNumber, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PinChargeCategoryDto>> GetPinCategoriesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PackageTypeDto>> GetPackageTypesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProviderChannelDto>> GetChannelsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ProviderPurchaseResultDto> PurchaseAsync(ProviderPurchaseRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProviderTraceResultDto> TraceAsync(ProviderTraceRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProviderBalanceDto> GetBalanceAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProviderErrorDto>> GetErrorsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ProviderReportDto> GetTransactionReportAsync(ProviderReportRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProviderReportDto> GetWalletChargeReportAsync(ProviderReportRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class EmptyMarkupRuleRepository : IChargeMarkupRuleRepository
    {
        public Task<ChargeMarkupRule?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ChargeMarkupRule?>(null);
        public Task<IReadOnlyList<ChargeMarkupRule>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeMarkupRule>>([]);
        public Task<ChargeMarkupRule?> FindApplicableAsync(ChargeOperator @operator, ChargeServiceType serviceType, DateTime nowUtc, CancellationToken ct = default) => Task.FromResult<ChargeMarkupRule?>(null);
        public Task<bool> HasOverlapAsync(Guid? excludingId, ChargeOperator? @operator, ChargeServiceType? serviceType, DateTime from, DateTime? to, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(ChargeMarkupRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChargeRequestRepository : IChargeRequestRepository
    {
        public int AddCount { get; private set; }
        public Task<ChargeRequest?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<ChargeRequest?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default) => Task.FromResult<ChargeRequest?>(null);
        public Task<IReadOnlyList<ChargeRequest>> GetWorkItemsAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([]);
        public Task<IReadOnlyList<ChargeRequest>> GetExpiredCandidatesAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ChargeRequest>>([]);
        public Task AddAsync(ChargeRequest request, CancellationToken ct = default) { AddCount++; return Task.CompletedTask; }
        public Task AddFulfillmentAttemptAsync(ChargeFulfillmentAttempt attempt, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
