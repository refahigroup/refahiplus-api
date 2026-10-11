using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Features.Catalogue;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;
using Xunit;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;
using Refahi.Modules.Orders.Application.Contracts.Payments;

namespace Refahi.Modules.Hotels.Tests;

public sealed class HotelCatalogueTests
{
    private static HotelTravel Travel => new(DateOnly.FromDateTime(DateTime.Now.AddDays(3)), DateOnly.FromDateTime(DateTime.Now.AddDays(5)), [new(2, [])]);
    private static ProviderHotelOffer Offer(string id = "1", long price = 1000) => new(new(id, "1", "Same Hotel", "Address", 4, []), "1", "Room", "rate", "BedBreakfast", 2, price, 900, 10);

    [Fact]
    public async Task DuplicateHotelsFromTwoProviders_HaveDistinctKeysAndBothAppear()
    {
        var repo = new MemoryCatalogue(); var snapp = new FakeProvider("SnappTrip", [Offer()]); var lamasoo = new FakeProvider("Lamasoo", [Offer()]);
        var service = Service(repo, snapp, lamasoo);
        var response = await service.SearchAsync(new(null, 1, Travel), default);
        Assert.Equal(2, response.Offers.Count);
        Assert.Equal(2, response.Offers.Select(x => x.HotelKey).Distinct().Count());
        Assert.Equal(2, response.Offers.Select(x => x.OfferToken).Distinct().Count());
        Assert.Single(repo.Cities.Select(x => x.CityKey).Distinct());
        Assert.False(response.IsPartial);
    }

    [Fact]
    public async Task OneFailedProvider_ReturnsHealthyResultsAndPartialMessage()
    {
        var snapp = new FakeProvider("SnappTrip", [Offer()]); var lamasoo = new FakeProvider("Lamasoo", []) { Failure = new HttpRequestException() };
        var response = await Service(new(), snapp, lamasoo).SearchAsync(new(null, 1, Travel), default);
        Assert.Single(response.Offers); Assert.True(response.IsPartial); Assert.NotNull(response.Message);
    }

    [Fact]
    public async Task AllFailedProviders_AreServiceFailureRatherThanSuccessfulEmptySearch()
    {
        var snapp = new FakeProvider("SnappTrip", []) { Failure = new HttpRequestException() };
        var lamasoo = new FakeProvider("Lamasoo", []) { Failure = new HttpRequestException() };
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(new(), snapp, lamasoo).SearchAsync(new(null, 1, Travel), default));
    }

    [Fact]
    public async Task MissingCityMapping_IsNotCountedAsAHealthyProviderResponse()
    {
        var repo = new MemoryCatalogue(); var key = Guid.NewGuid(); repo.AddCity(HotelCityReference.Create(key, "SnappTrip", "1", "City", 1));
        var snapp = new FakeProvider("SnappTrip", []) { Failure = new HttpRequestException() };
        var lamasoo = new FakeProvider("Lamasoo", []) { CityId = "unmapped", LegacyId = null };
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(repo, snapp, lamasoo).SearchAsync(new(key, null, Travel), default));
        Assert.Equal(0, lamasoo.SearchCalls);
    }

    [Fact]
    public async Task EmptySuccessfulProviderResponse_RemainsEmptySuccess()
    {
        var response = await Service(new(), new FakeProvider("SnappTrip", [])).SearchAsync(new(null, 1, Travel), default);
        Assert.Empty(response.Offers); Assert.False(response.IsPartial);
    }

    [Fact]
    public async Task DetailsRemainWithSelectedProvider_AndFilteringIsOnCombinedResults()
    {
        var repo = new MemoryCatalogue(); var snapp = new FakeProvider("SnappTrip", [Offer(price: 1000)]); var lamasoo = new FakeProvider("Lamasoo", [Offer(price: 2000)]);
        var service = Service(repo, snapp, lamasoo);
        var search = await service.SearchAsync(new(null, 1, Travel, MinPrice: 1500), default);
        var selected = Assert.Single(search.Offers);
        var details = await service.DetailsAsync(selected.HotelKey, Travel, default);
        Assert.Single(details.Availability.Offers); Assert.Equal(0, snapp.DetailsCalls); Assert.Equal(1, lamasoo.DetailsCalls);
    }

    [Fact]
    public async Task CancellationPropagates_AndIsNotReportedAsPartialSuccess()
    {
        var provider = new FakeProvider("SnappTrip", []) { WaitForCancellation = true };
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new(), provider).SearchAsync(new(null, 1, Travel), ct.Token));
    }

    [Fact]
    public async Task DiscoveryCacheIncludesChildAges_FreshQuotesBypassCache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions()); var raw = new FakeProvider("SnappTrip", [Offer()]);
        var provider = new BoundedHotelCatalogueProvider(raw, cache, 8);
        await provider.SearchAsync("1", Travel, default); await provider.SearchAsync("1", Travel, default);
        Assert.Equal(1, raw.SearchCalls);
        await provider.SearchAsync("1", Travel with { Rooms = [new(2, [3])] }, default);
        Assert.Equal(2, raw.SearchCalls);
        await provider.SearchFreshAsync("1", Travel, default);
        Assert.Equal(3, raw.SearchCalls);
    }

    [Fact]
    public async Task ProviderDeadlineIsIndependent_AndCancelsUnderlyingRead()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions()); var raw = new FakeProvider("Slow", []) { WaitForCancellation = true };
        var provider = new BoundedHotelCatalogueProvider(raw, cache, 1);
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => provider.SearchAsync("1", Travel, default));
    }

    [Fact]
    public async Task QuoteUsesSnapshotProviderAndNativeRateReferences()
    {
        var repo = new MemoryCatalogue(); var snapp = new FakeProvider("SnappTrip", [Offer()]); var lamasoo = new FakeProvider("Lamasoo", [Offer()]);
        var service = Service(repo, snapp, lamasoo); var response = await service.SearchAsync(new(null, 1, Travel), default);
        var chosen = response.Offers.Single(x => repo.Offers.Single(s => s.Token == x.OfferToken).Provider == "Lamasoo");
        lamasoo.Offers = [Offer(price: 1200)];
        var quote = await service.QuoteAsync(chosen.OfferToken, Travel, default);
        Assert.Equal(1200, quote.PriceMinor); Assert.Equal(1, snapp.SearchCalls); Assert.Equal(2, lamasoo.SearchCalls);
        Assert.NotEqual(chosen.OfferToken, quote.OfferToken);
    }

    [Fact]
    public async Task SubmitIsPersistedBeforeNetwork_AndReplayDoesNotSubmitAgain()
    {
        var state = new RequestHarness();
        state.Provider.OnSubmit = submitted =>
        {
            var local = Assert.Single(state.Requests.Rows);
            var external = Assert.Single(state.Catalogue.Reservations);
            Assert.Equal(local.Id.ToString("N"), submitted.ReferenceId);
            Assert.Equal(HotelExternalState.Submitting, external.State);
            return new(submitted.ReferenceId, 900, 9, DateTime.UtcNow.AddMinutes(15), "{}");
        };
        var response = await state.Service.CreateAsync(state.Command, default);
        var again = await state.Service.CreateAsync(state.Command, default);
        Assert.Equal(response.RequestId, again.RequestId);
        Assert.Equal(1, state.Provider.SubmitCalls);
        Assert.Equal(HotelExternalState.Reserved, Assert.Single(state.Catalogue.Reservations).State);
        Assert.Equal(1000, Assert.Single(state.Requests.Rows).TotalPrice);
        Assert.Equal(900, Assert.Single(state.Catalogue.Reservations).SupplyPriceMinor);
        Assert.Null(Assert.Single(state.Requests.Rows).OrderId);
    }

    [Fact]
    public async Task TimeoutAfterSendingSubmit_PreservesStableReference_AndReplayCannotResubmit()
    {
        var state = new RequestHarness(); state.Provider.OnSubmit = _ => throw new HotelExternalOutcomeUnknownException("unknown");
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.CreateAsync(state.Command, default));
        await Assert.ThrowsAsync<HotelCatalogueConflictException>(() => state.Service.CreateAsync(state.Command, default));
        Assert.Equal(1, state.Provider.SubmitCalls);
        Assert.Equal(HotelExternalState.Unknown, Assert.Single(state.Catalogue.Reservations).State);
    }

    [Fact]
    public async Task ReusedIdempotencyKeyWithDifferentData_IsConflict()
    {
        var state = new RequestHarness();
        await state.Service.CreateAsync(state.Command, default);
        await Assert.ThrowsAsync<HotelCatalogueConflictException>(() => state.Service.CreateAsync(state.Command with { Phone = "09111111111" }, default));
        Assert.Equal(1, state.Provider.SubmitCalls);
    }

    [Fact]
    public async Task PurchaseDisabled_AndPriceTampering_AreRejectedBeforeSubmission()
    {
        var state = new RequestHarness(); state.Provider.Purchase = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Service.CreateAsync(state.Command, default));
        state.Provider.Purchase = true;
        await Assert.ThrowsAsync<Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest.HotelPriceChangedException>(
            () => state.Service.CreateAsync(state.Command with { ExpectedTotalPriceMinor = 1 }, default));
        Assert.Empty(state.Requests.Rows); Assert.Equal(0, state.Provider.SubmitCalls);
    }

    [Fact]
    public async Task PaymentRejectsUnknownExpiredAndWrongOwner_BeforeWalletDebit()
    {
        var state = new RequestHarness(); var response = await state.Service.CreateAsync(state.Command, default);
        var request = state.Requests.Rows.Single(); var orderId = Guid.NewGuid(); request.ConvertToOrder(orderId, DateTime.UtcNow);
        var participant = new HotelPaymentParticipant(state.Catalogue, state.Requests);
        var context = new OrderPaymentContext(orderId, request.UserId, response.RequestId, request.TotalPrice);
        Assert.Null(await participant.GetUnavailableReasonAsync(context, default));
        Assert.NotNull(await participant.GetUnavailableReasonAsync(context with { UserId = Guid.NewGuid() }, default));
        Assert.NotNull(await participant.GetUnavailableReasonAsync(context with { TotalMinor = 1 }, default));
        state.Catalogue.Reservations.Single().MarkUnknown();
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.AcquireAsync(context, default));
    }

    [Fact]
    public async Task ConcurrentClicksWithSameKey_CreateOneReservation()
    {
        var state = new RequestHarness();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Provider.OnSubmitAsync = async (request, ct) =>
        {
            started.SetResult(); await release.Task.WaitAsync(ct);
            return new(request.ReferenceId, 900, 9, DateTime.UtcNow.AddMinutes(15), "{}");
        };
        var first = state.Service.CreateAsync(state.Command, default);
        await started.Task;
        var second = state.Service.CreateAsync(state.Command, default);
        release.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(results[0].RequestId, results[1].RequestId);
        Assert.Single(state.Requests.Rows); Assert.Single(state.Catalogue.Reservations); Assert.Equal(1, state.Provider.SubmitCalls);
    }

    [Fact]
    public async Task ConfirmSuccessAndPaidEventReplay_CompleteExactlyOnce()
    {
        var state = new RequestHarness(); var response = await state.Service.CreateAsync(state.Command, default);
        var request = state.Requests.Rows.Single(); var orderId = Guid.NewGuid(); request.ConvertToOrder(orderId, DateTime.UtcNow);
        await state.Service.FinalizeAsync(orderId, request.UserId, default);
        await state.Service.FinalizeAsync(orderId, request.UserId, default);
        Assert.Equal(1, state.Provider.ConfirmCalls);
        Assert.Equal(HotelExternalState.Confirmed, state.Catalogue.Reservations.Single().State);
        Assert.Equal("reservation-final", request.ProviderBookingCode);
    }

    [Fact]
    public async Task ConfirmTimeout_BlocksRepeatedConfirmAndAutomaticRefund()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        state.Provider.OnConfirm = _ => throw new HotelExternalOutcomeUnknownException("unknown");
        var request = state.Requests.Rows.Single(); var orderId = Guid.NewGuid(); request.ConvertToOrder(orderId, DateTime.UtcNow);
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.FinalizeAsync(orderId, request.UserId, default));
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.FinalizeAsync(orderId, request.UserId, default));
        Assert.Equal(1, state.Provider.ConfirmCalls);
        Assert.Equal(HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
        var cancellation = new HotelCancellationParticipant(state.Catalogue, null!, null!);
        var preparation = await cancellation.PrepareAsync(new(orderId, request.UserId, "Hotel", request.Id, "HotelRequest", "Paid", "test"), default);
        Assert.False(preparation.CanContinue);
    }

    [Fact]
    public async Task TooShortSubmitLock_IsUnknownAndNeverPayable()
    {
        var state = new RequestHarness();
        state.Provider.OnSubmit = request => new(request.ReferenceId, 900, 9, DateTime.UtcNow.AddSeconds(20), "{}");
        await Assert.ThrowsAnyAsync<Exception>(() => state.Service.CreateAsync(state.Command, default));
        Assert.Equal(HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
        Assert.Null(state.Requests.Rows.Single().OrderId);
    }

    private sealed class RequestHarness
    {
        public MemoryCatalogue Catalogue { get; } = new();
        public MemoryRequests Requests { get; } = new();
        public FakeProvider Provider { get; } = new("Lamasoo", [Offer()]) { Purchase = true };
        public HotelCatalogueService Service { get; }
        public CreateHotelCatalogueRequestCommand Command { get; }
        public RequestHarness()
        {
            Catalogue.AddOffer(HotelOfferSnapshot.Create("test-token", Guid.NewGuid(), "Lamasoo", HotelCatalogueService.Serialize(Offer()),
                HotelCatalogueService.Serialize(Travel), DateTime.UtcNow.AddMinutes(20)));
            Service = new(Catalogue, new Registry([Provider]), new HotelSalePriceCalculator(), Requests, new MemorySagas(), new PaidMediator(Requests), NullLogger<HotelCatalogueService>.Instance);
            Command = new(Guid.NewGuid(), "test-token", Travel,
                [new("First", "Last", "1234567891", "IR", "Adult", 30, 0), new("Second", "Last", "1234567891", "IR", "Adult", 31, 0)],
                "09123456789", null, 1000, "stable-key");
        }
    }
    private sealed class MemoryRequests : IHotelRequestRepository
    {
        public List<HotelRequest> Rows { get; } = [];
        public Task<HotelRequest?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(x => x.Id == id));
        public Task<HotelRequest?> GetForUserAsync(Guid id, Guid user, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(x => x.Id == id && x.UserId == user));
        public Task<HotelRequest?> GetByOrderIdAsync(Guid order, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(x => x.OrderId == order));
        public Task<HotelRequest?> GetByIdempotencyKeyAsync(Guid user, string key, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(x => x.UserId == user && x.IdempotencyKey == key));
        public Task AddAsync(HotelRequest row, CancellationToken ct = default) { Rows.Add(row); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class MemorySagas : IHotelBookingSagaRepository
    {
        private readonly List<HotelBookingSagaState> rows = [];
        public Task<HotelBookingSagaState?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(rows.SingleOrDefault(x => x.SagaId == id));
        public Task<HotelBookingSagaState?> GetByHotelRequestIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(rows.SingleOrDefault(x => x.HotelRequestId == id));
        public Task<HotelBookingSagaState?> GetByOrderIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(rows.SingleOrDefault(x => x.OrderId == id));
        public Task<IReadOnlyList<HotelBookingSagaState>> GetStuckAsync(IReadOnlyCollection<HotelBookingSagaStatus> statuses, DateTime before, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<HotelBookingSagaState>>([]);
        public Task AddAsync(HotelBookingSagaState row, CancellationToken ct = default) { rows.Add(row); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static HotelCatalogueService Service(MemoryCatalogue repository, params FakeProvider[] providers) =>
        new(repository, new Registry(providers), new HotelSalePriceCalculator(), null!, null!, null!, NullLogger<HotelCatalogueService>.Instance);

    private sealed class Registry(IReadOnlyList<FakeProvider> providers) : IHotelCatalogueProviderRegistry
    {
        public IReadOnlyList<IHotelCatalogueProvider> Active => providers;
        public IHotelCatalogueProvider Get(string name) => providers.Single(x => x.Name == name);
    }
    private sealed class FakeProvider(string name, IReadOnlyList<ProviderHotelOffer> offers) : IHotelCatalogueProvider
    {
        public string Name => name;
        public string CityId { get; set; } = "1";
        public int? LegacyId { get; set; } = 1;
        public IReadOnlyList<ProviderHotelOffer> Offers { get; set; } = offers;
        public Exception? Failure { get; set; }
        public bool WaitForCancellation { get; set; }
        public int SearchCalls { get; private set; }
        public int DetailsCalls { get; private set; }
        public bool Purchase { get; set; } = name == "SnappTrip";
        public int SubmitCalls { get; private set; }
        public Func<HotelSubmitRequest, HotelSubmitResult> OnSubmit { get; set; } = submitted => new(submitted.ReferenceId, 900, 9, DateTime.UtcNow.AddMinutes(15), "{}");
        public HotelProviderCapabilities Capabilities => new(true, Purchase, false, false, false);
        public Task<IReadOnlyList<ProviderCity>> CitiesAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<ProviderCity>>([new(CityId, "City", "C", "IR", LegacyId)]); }
        public async Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string cityId, HotelTravel travel, CancellationToken ct)
        {
            SearchCalls++; if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            if (Failure is not null) throw Failure; return Offers;
        }
        public Task<ProviderHotel> DetailsAsync(string id, string cityId, CancellationToken ct) { DetailsCalls++; return Task.FromResult(Offers.Single(x => x.Hotel.Id == id).Hotel); }
        public Func<HotelSubmitRequest, CancellationToken, Task<HotelSubmitResult>>? OnSubmitAsync { get; set; }
        public async Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct)
        { SubmitCalls++; return OnSubmitAsync is null ? OnSubmit(request) : await OnSubmitAsync(request, ct); }
        public int ConfirmCalls { get; private set; }
        public Func<string, HotelConfirmResult> OnConfirm { get; set; } = _ => new("reservation-final", "Confirmed");
        public Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct) { ConfirmCalls++; return Task.FromResult(OnConfirm(referenceId)); }
    }

    private sealed class PaidMediator(MemoryRequests requests) : MediatR.IMediator
    {
        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> query, CancellationToken ct = default)
        {
            if (query is not Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderByIdQuery) throw new NotSupportedException("No refunds or new orders expected.");
            var request = requests.Rows.Single();
            var order = new Refahi.Modules.Orders.Application.Contracts.Dtos.OrderDto(request.OrderId!.Value, "TEST", request.UserId,
                request.TotalPrice, 0, 0, null, 0, request.TotalPrice, "Processing", "Paid", "Hotel", request.Id, "HotelRequest", null, null, null, 0, [], DateTimeOffset.UtcNow);
            return Task.FromResult((TResponse)(object)order);
        }
        public Task<object?> Send(object query, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(MediatR.IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<T>(T notification, CancellationToken ct = default) where T : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class MemoryCatalogue : IHotelCatalogueRepository
    {
        public List<HotelCityReference> Cities { get; } = [];
        public List<HotelListing> Listings { get; } = [];
        public List<HotelOfferSnapshot> Offers { get; } = [];
        public List<HotelExternalReservation> Reservations { get; } = [];
        public Task<IReadOnlyList<HotelCityReference>> CitiesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelCityReference>>(Cities);
        public Task<HotelListing> ListingAsync(Guid city, string provider, string native, string nativeCity, CancellationToken ct)
        {
            var row = Listings.FirstOrDefault(x => x.Provider == provider && x.NativeId == native);
            if (row is null) { row = HotelListing.Create(city, provider, native, nativeCity); Listings.Add(row); }
            return Task.FromResult(row);
        }
        public Task<HotelListing?> FindListingAsync(Guid key, CancellationToken ct) => Task.FromResult(Listings.FirstOrDefault(x => x.HotelKey == key));
        public Task<HotelOfferSnapshot?> OfferAsync(string token, CancellationToken ct) => Task.FromResult(Offers.FirstOrDefault(x => x.Token == token));
        public Task<HotelExternalReservation?> ReservationAsync(Guid id, CancellationToken ct) => Task.FromResult(Reservations.SingleOrDefault(x => x.HotelRequestId == id));
        public Task<IReadOnlyList<HotelExternalReservation>> RecoveryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelExternalReservation>>([]);
        public void AddCity(HotelCityReference row) => Cities.Add(row);
        public void AddOffer(HotelOfferSnapshot row) => Offers.Add(row);
        public void AddReservation(HotelExternalReservation row) => Reservations.Add(row);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> locks = new();
        public async Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct)
        {
            var semaphore = locks.GetOrAdd(key, _ => new(1)); await semaphore.WaitAsync(ct); return new Lease(semaphore);
        }
        private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
        { public ValueTask DisposeAsync() { semaphore.Release(); return ValueTask.CompletedTask; } }
    }
}
