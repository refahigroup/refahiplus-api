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
    public async Task ReferenceCities_AreReadOnly_AndNormalizePersianAndEnglishNames()
    {
        var repo = new MemoryCatalogue();
        var provider = new FakeProvider("SnappTrip", []) { Failure = new HttpRequestException() };
        var service = Service(repo, provider);
        var city = Assert.Single(await service.CitiesAsync("  كيش  ", default));
        Assert.Equal(1, city.ReferenceCityId); Assert.Equal("هرمزگان", city.ProvinceName);
        Assert.Single(await service.CitiesAsync("KISH", default));
        Assert.Single(await service.CitiesAsync(null, default));
        Assert.Equal(0, provider.SearchCalls); Assert.Empty(repo.Cities);
        Assert.Equal(0, repo.SaveCalls); Assert.Equal(0, repo.LockCalls);
    }

    [Fact]
    public async Task AllActiveReferenceCitiesRemainVisibleWithoutMappingsIncludingForeignDestinations()
    {
        var repo = new MemoryCatalogue();
        var mediator = new PaidMediator(new MemoryRequests(),
        [
            new(1, "رضوانشهر", "Rezvanshahr", "one", 1, "گیلان", 0, true),
            new(2, "رضوانشهر", "Rezvanshahr", "two", 2, "اصفهان", 1, true),
            new(3, "استانبول", "Istanbul", "istanbul", 40, "ترکیه", 2, true),
            new(4, "غیرفعال", "Inactive", "inactive", 1, "گیلان", 3, false)
        ]);
        var service = new HotelCatalogueService(repo, new Registry([]), new HotelSalePriceCalculator(), null!, null!, mediator, NullLogger<HotelCatalogueService>.Instance);
        var cities = await service.CitiesAsync(null, default);
        Assert.Equal([1, 2, 3], cities.Select(x => x.ReferenceCityId));
        Assert.Equal(2, (await service.CitiesAsync("رضوانشهر", default)).Count);
        Assert.Single(await service.CitiesAsync("Istanbul", default));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.SearchAsync(new(4, Travel), default));
        Assert.Equal(0, repo.SaveCalls); Assert.Equal(0, repo.LockCalls);
    }

    [Fact]
    public async Task MissingMappings_ReturnEmptySuccess_AndNeverSearchProvider()
    {
        var provider = new FakeProvider("Lamasoo", []) { LegacyId = null };
        var result = await Service(new(), provider).SearchAsync(new(1, Travel), default);
        Assert.Empty(result.Offers); Assert.False(result.IsPartial); Assert.Equal(0, provider.SearchCalls);
    }

    [Fact]
    public async Task MultipleNativeMappings_AreAllSearched_AndExactOffersAreDeduplicated()
    {
        var provider = new FakeProvider("SnappTrip", [Offer()]) { NativeIds = ["native-a", "native-b", "native-a"] };
        var result = await Service(new(), provider).SearchAsync(new(1, Travel), default);
        Assert.Equal(["native-a", "native-b"], provider.SearchedCities);
        Assert.Single(result.Offers);
    }

    [Fact]
    public async Task PartialFailureWithinOneProvider_DoesNotDiscardItsSuccessfulOffers()
    {
        var provider = new FakeProvider("SnappTrip", [Offer()]) { NativeIds = ["healthy", "failed"], FailedCity = "failed" };
        var result = await Service(new(), provider).SearchAsync(new(1, Travel), default);
        Assert.Single(result.Offers); Assert.True(result.IsPartial);
    }

    [Fact]
    public async Task MappingDatabaseFailure_IsUnavailable_NotEmptySuccess()
    {
        var provider = new FakeProvider("Lamasoo", []) { MappingFailure = new InvalidOperationException("mapping unavailable") };
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(new(), provider).SearchAsync(new(1, Travel), default));
        Assert.Equal(0, provider.SearchCalls);
    }

    [Fact]
    public async Task InvalidReferenceCity_IsRejectedBeforeProviderSearch()
    {
        var provider = new FakeProvider("SnappTrip", [Offer()]);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => Service(new(), provider).SearchAsync(new(6433, Travel), default));
        Assert.Equal(0, provider.SearchCalls);
    }

    [Fact]
    public async Task NoActiveProviders_IsUnavailable()
    {
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(new()).SearchAsync(new(1, Travel), default));
    }

    [Fact]
    public async Task DuplicateHotelsFromTwoProviders_HaveDistinctKeysAndBothAppear()
    {
        var repo = new MemoryCatalogue(); var snapp = new FakeProvider("SnappTrip", [Offer()]); var lamasoo = new FakeProvider("Lamasoo", [Offer()]);
        var service = Service(repo, snapp, lamasoo);
        var response = await service.SearchAsync(new(1, Travel), default);
        Assert.Equal(2, response.Offers.Count);
        Assert.Equal(2, response.Offers.Select(x => x.HotelKey).Distinct().Count());
        Assert.Equal(2, response.Offers.Select(x => x.OfferToken).Distinct().Count());
        Assert.Empty(repo.Cities);
        Assert.False(response.IsPartial);
    }

    [Fact]
    public async Task OneFailedProvider_ReturnsHealthyResultsAndPartialMessage()
    {
        var snapp = new FakeProvider("SnappTrip", [Offer()]); var lamasoo = new FakeProvider("Lamasoo", []) { Failure = new HttpRequestException() };
        var response = await Service(new(), snapp, lamasoo).SearchAsync(new(1, Travel), default);
        Assert.Single(response.Offers); Assert.True(response.IsPartial); Assert.NotNull(response.Message);
    }

    [Fact]
    public async Task AllFailedProviders_AreServiceFailureRatherThanSuccessfulEmptySearch()
    {
        var snapp = new FakeProvider("SnappTrip", []) { Failure = new HttpRequestException() };
        var lamasoo = new FakeProvider("Lamasoo", []) { Failure = new HttpRequestException() };
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(new(), snapp, lamasoo).SearchAsync(new(1, Travel), default));
    }

    [Fact]
    public async Task MissingCityMapping_IsNotCountedAsAHealthyProviderResponse()
    {
        var repo = new MemoryCatalogue(); var key = Guid.NewGuid(); repo.AddCity(HotelCityReference.Create(key, "SnappTrip", "1", "City", 1));
        var snapp = new FakeProvider("SnappTrip", []) { Failure = new HttpRequestException() };
        var lamasoo = new FakeProvider("Lamasoo", []) { CityId = "unmapped", LegacyId = null };
        await Assert.ThrowsAsync<HotelCatalogueUnavailableException>(() => Service(repo, snapp, lamasoo).SearchAsync(new(1, Travel), default));
        Assert.Equal(0, lamasoo.SearchCalls);
    }

    [Fact]
    public async Task EmptySuccessfulProviderResponse_RemainsEmptySuccess()
    {
        var response = await Service(new(), new FakeProvider("SnappTrip", [])).SearchAsync(new(1, Travel), default);
        Assert.Empty(response.Offers); Assert.False(response.IsPartial);
    }

    [Fact]
    public async Task DetailsRemainWithSelectedProvider_AndFilteringIsOnCombinedResults()
    {
        var repo = new MemoryCatalogue(); var snapp = new FakeProvider("SnappTrip", [Offer(price: 1000)]); var lamasoo = new FakeProvider("Lamasoo", [Offer(price: 2000)]);
        var service = Service(repo, snapp, lamasoo);
        var search = await service.SearchAsync(new(1, Travel, MinPrice: 1500), default);
        var selected = Assert.Single(search.Offers);
        var details = await service.DetailsAsync(selected.HotelKey, Travel, default);
        Assert.Single(details.Availability.Offers); Assert.Equal(0, snapp.DetailsCalls); Assert.Equal(1, lamasoo.DetailsCalls);
    }

    [Fact]
    public async Task CancellationPropagates_AndIsNotReportedAsPartialSuccess()
    {
        var provider = new FakeProvider("SnappTrip", []) { WaitForCancellation = true };
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new(), provider).SearchAsync(new(1, Travel), ct.Token));
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
        var service = Service(repo, snapp, lamasoo); var response = await service.SearchAsync(new(1, Travel), default);
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
        var response = await state.Service.CreateAsync(state.Command, default);
        var replay = await state.Service.CreateAsync(state.Command, default);
        Assert.Equal(response.RequestId, replay.RequestId);
        Assert.Equal("NeedsReview", replay.BookingStatus); Assert.False(replay.CanProceedToPayment);
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
        var response = await state.Service.CreateAsync(state.Command, default);
        Assert.Equal("NeedsReview", response.BookingStatus); Assert.False(response.CanProceedToPayment);
        Assert.Equal(HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
        Assert.Null(state.Requests.Rows.Single().OrderId);
    }

    [Fact]
    public async Task SupplyLossBeforeSubmit_RejectsWithoutCreatingRequest()
    {
        var state = new RequestHarness(); state.Provider.Offers = [Offer() with { SupplyPriceMinor = 1001 }];
        await Assert.ThrowsAsync<HotelCatalogueConflictException>(() => state.Service.CreateAsync(state.Command, default));
        Assert.Empty(state.Requests.Rows); Assert.Equal(0, state.Provider.SubmitCalls);
    }

    [Fact]
    public async Task ActualSupplyLoss_IsDurableAndCannotCreateOrderOrDebit()
    {
        var state = new RequestHarness(); state.Provider.OnSubmit = x => new(x.ReferenceId, 1100, 10, DateTime.UtcNow.AddMinutes(15), "{}");
        var response = await state.Service.CreateAsync(state.Command, default);
        Assert.False(response.CanProceedToPayment); Assert.Equal("NeedsReview", response.BookingStatus);
        var row = state.Requests.Rows.Single(); Assert.Equal(1000, row.TotalPrice);
        var external = state.Catalogue.Reservations.Single(); Assert.Equal(1090, external.HotelShareMinor); Assert.True(external.PaymentBlocked);
        var handler = new Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler(
            state.Requests, new MemorySagas(), state.Mediator,
            NullLogger<Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler>.Instance, state.Catalogue);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(row.Id, row.UserId, "convert"), default));
        Assert.Null(row.OrderId);
    }

    [Fact]
    public async Task ExistingOrderReplay_ReturnsOrderEvenWhenReservationIsUnknownOrRequestFailed()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); var order = Guid.NewGuid(); row.ConvertToOrder(order, DateTime.UtcNow); row.MarkFailed(DateTime.UtcNow);
        state.Catalogue.Reservations.Single().MarkUnknown();
        var handler = new Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler(
            state.Requests, new MemorySagas(), state.Mediator,
            NullLogger<Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler>.Instance, state.Catalogue);
        Assert.Equal(order, (await handler.Handle(new(row.Id, row.UserId, "convert"), default)).OrderId);
    }

    [Fact]
    public async Task DefinitiveSubmitRejection_IsNotAnUnknownTimeout()
    {
        var state = new RequestHarness(); state.Provider.OnSubmit = _ => throw new HotelProviderRejectedException("OFFER_UNAVAILABLE");
        var response = await state.Service.CreateAsync(state.Command, default);
        Assert.Equal("Failed", response.BookingStatus); Assert.False(response.CanProceedToPayment);
        Assert.Equal(HotelExternalOperationState.Rejected, Assert.Single(state.Catalogue.Operations).State);
        await state.Service.CreateAsync(state.Command, default); Assert.Equal(1, state.Provider.SubmitCalls);
    }

    [Fact]
    public async Task Status_EnforcesOwnershipAndSeparatesPaymentFromBooking()
    {
        var state = new RequestHarness(); var created = await state.Service.CreateAsync(state.Command, default);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => state.Service.StatusAsync(new(created.RequestId, Guid.NewGuid()), default));
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        var status = await state.Service.StatusAsync(new(row.Id, row.UserId), default);
        Assert.Equal("Paid", status.PaymentState); Assert.Equal("Finalizing", status.BookingStatus); Assert.Null(status.ReservationNumber); Assert.False(status.CanProceedToPayment);
        await state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default);
        status = await state.Service.StatusAsync(new(row.Id, Guid.NewGuid(), true), default);
        Assert.Equal("Confirmed", status.BookingStatus); Assert.Equal("reservation-final", status.ReservationNumber);
    }

    [Fact]
    public async Task UnknownWithoutInquiry_RemainsUnresolvedAndNeverRetriesMutationOrRefund()
    {
        var state = new RequestHarness(); state.Provider.OnSubmit = _ => throw new HotelExternalOutcomeUnknownException("timeout");
        await state.Service.CreateAsync(state.Command, default);
        await state.Service.RecoverAsync(default, true);
        Assert.Equal(HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
        Assert.Equal(1, state.Provider.SubmitCalls); Assert.Equal(0, state.Provider.ConfirmCalls); Assert.Empty(state.Mediator.RefundKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InquiryConfirmed_RequiresPaidOrderToComplete(bool paid)
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        state.Mediator.Payment = paid ? "Paid" : "Unpaid";
        state.Provider.OnConfirm = _ => throw new HotelExternalOutcomeUnknownException("timeout");
        if (paid) await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default));
        else state.Catalogue.Reservations.Single().MarkUnknown();
        state.Provider.Inquiry = new(HotelInquiryState.Confirmed, "verified-final", Snapshot: "{}", OperationCompleted: true);
        await state.Service.RecoverAsync(default, true);
        Assert.Equal(paid ? HotelExternalState.Confirmed : HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
        Assert.Equal(paid ? "verified-final" : null, row.ProviderBookingCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservedInquiryAfterConfirmTimeout_RequiresExplicitRetrySafety(bool safe)
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        state.Provider.OnConfirm = _ => throw new HotelExternalOutcomeUnknownException("timeout");
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default));
        state.Provider.Inquiry = new(HotelInquiryState.Reserved, LockedUntilUtc: DateTime.UtcNow.AddMinutes(10), SupplyPriceMinor: 900, FeeMinor: 9,
            Snapshot: "{}", OperationCompleted: true, SafeToConfirmAgain: safe);
        state.Provider.OnConfirm = _ => new("verified-final", "Confirmed");
        await state.Service.RecoverAsync(default, true);
        Assert.Equal(safe ? 2 : 1, state.Provider.ConfirmCalls);
        Assert.Equal(safe ? HotelExternalState.Confirmed : HotelExternalState.Unknown, state.Catalogue.Reservations.Single().State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionInquiry_RefundsOnlyWithProofOfNoExternalLiability(bool noLiability)
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        state.Catalogue.Reservations.Single().MarkUnknown();
        state.Provider.Inquiry = new(HotelInquiryState.Cancelled, Snapshot: "{}", OperationCompleted: true, NoExternalLiability: noLiability);
        await state.Service.RecoverAsync(default, true);
        if (noLiability)
        {
            Assert.Equal($"hotel-external-compensation-{row.Id:N}", Assert.Single(state.Mediator.RefundKeys));
            var cancellation = new HotelCancellationParticipant(state.Catalogue, null!, null!);
            Assert.True((await cancellation.PrepareAsync(new(row.OrderId.Value, row.UserId, "Hotel", row.Id, "HotelRequest", "Paid", "test"), default)).CanContinue);
        }
        else Assert.Empty(state.Mediator.RefundKeys);
    }

    [Fact]
    public void ConfirmedStateCannotBeDowngradedByLateFailure()
    {
        var row = HotelExternalReservation.Create(Guid.NewGuid(), "Lamasoo", "hash", "{}"); row.StartSubmit();
        row.Reserve(100, 1, DateTime.UtcNow.AddMinutes(10), "{}"); row.StartConfirm(); row.Confirm("final"); row.MarkUnknown();
        Assert.Equal(HotelExternalState.Confirmed, row.State); Assert.Equal("final", row.ReservationId);
    }

    [Fact]
    public async Task CrashAfterStartingConfirm_IsRecoveredAsUnknownWithoutRepeatingConfirm()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        var external = state.Catalogue.Reservations.Single(); external.StartConfirm();
        var operation = HotelExternalOperation.Start(external, HotelExternalOperationKind.Confirm); state.Catalogue.AddOperation(operation);
        typeof(HotelExternalReservation).GetProperty(nameof(external.UpdatedAtUtc))!.SetValue(external, DateTime.UtcNow.AddMinutes(-2));
        typeof(HotelExternalOperation).GetProperty(nameof(operation.StartedAtUtc))!.SetValue(operation, DateTime.UtcNow.AddMinutes(-2));
        await state.Service.RecoverAsync(default);
        Assert.Equal(HotelExternalState.Unknown, external.State); Assert.Equal(HotelExternalOperationState.Unknown, operation.State);
        Assert.Equal(0, state.Provider.ConfirmCalls); Assert.Empty(state.Mediator.RefundKeys);
    }

    [Fact]
    public async Task PaymentDiscoveredAfterLockExpiry_DoesNotConfirmOrRefundBlindly()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        var external = state.Catalogue.Reservations.Single();
        typeof(HotelExternalReservation).GetProperty(nameof(external.LockedUntilUtc))!.SetValue(external, DateTime.UtcNow.AddSeconds(-1));
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default));
        Assert.Equal(HotelExternalState.Unknown, external.State); Assert.Equal(0, state.Provider.ConfirmCalls); Assert.Empty(state.Mediator.RefundKeys);
    }

    [Fact]
    public async Task ConcurrentPaidEvents_SendOnlyOneConfirm()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); row.ConvertToOrder(Guid.NewGuid(), DateTime.UtcNow);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Provider.OnConfirmAsync = async _ => { started.SetResult(); await release.Task; return new("final", "Confirmed"); };
        var first = state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default); await started.Task;
        var second = state.Service.FinalizeAsync(row.OrderId.Value, row.UserId, default); release.SetResult();
        await Task.WhenAll(first, second); Assert.Equal(1, state.Provider.ConfirmCalls);
        Assert.Equal(HotelExternalState.Confirmed, state.Catalogue.Reservations.Single().State);
    }

    [Fact]
    public async Task OrderCreatedBeforeCrash_IsReattachedEvenAfterHoldExpiry()
    {
        var state = new RequestHarness(); await state.Service.CreateAsync(state.Command, default);
        var row = state.Requests.Rows.Single(); var orderId = Guid.NewGuid();
        state.Mediator.OrphanOrder = new(orderId, "TEST", row.UserId, row.TotalPrice, 0, 0, null, 0, row.TotalPrice,
            "Pending", "Unpaid", "Hotel", row.Id, "HotelRequest", null, null, null, 0, [], DateTimeOffset.UtcNow);
        typeof(HotelRequest).GetProperty(nameof(row.ExpireAt))!.SetValue(row, DateTime.UtcNow.AddSeconds(-1));
        state.Catalogue.Reservations.Single().MarkUnknown();
        var handler = new Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler(
            state.Requests, new MemorySagas(), state.Mediator,
            NullLogger<Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder.ConvertHotelRequestToOrderCommandHandler>.Instance, state.Catalogue);
        var result = await handler.Handle(new(row.Id, row.UserId, "convert"), default);
        Assert.Equal(orderId, result.OrderId); Assert.Equal(orderId, row.OrderId);
        Assert.Equal(Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums.HotelRequestStatus.Expired, row.Status);
        Assert.Equal(1, state.Provider.SubmitCalls);
    }

    private sealed class RequestHarness
    {
        public MemoryCatalogue Catalogue { get; } = new();
        public MemoryRequests Requests { get; } = new();
        public FakeProvider Provider { get; } = new("Lamasoo", [Offer()]) { Purchase = true };
        public HotelCatalogueService Service { get; }
        public PaidMediator Mediator { get; }
        public CreateHotelCatalogueRequestCommand Command { get; }
        public RequestHarness()
        {
            Catalogue.AddOffer(HotelOfferSnapshot.Create("test-token", Guid.NewGuid(), "Lamasoo", HotelCatalogueService.Serialize(Offer()),
                HotelCatalogueService.Serialize(Travel), DateTime.UtcNow.AddMinutes(20)));
            Mediator = new(Requests);
            Service = new(Catalogue, new Registry([Provider]), new HotelSalePriceCalculator(), Requests, new MemorySagas(), Mediator, NullLogger<HotelCatalogueService>.Instance);
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
        new(repository, new Registry(providers), new HotelSalePriceCalculator(), null!, null!, new PaidMediator(new MemoryRequests()), NullLogger<HotelCatalogueService>.Instance);

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
        public Exception? MappingFailure { get; set; }
        public string? FailedCity { get; set; }
        public bool WaitForCancellation { get; set; }
        public int SearchCalls { get; private set; }
        public int DetailsCalls { get; private set; }
        public bool Purchase { get; set; } = name == "SnappTrip";
        public int SubmitCalls { get; private set; }
        public Func<HotelSubmitRequest, HotelSubmitResult> OnSubmit { get; set; } = submitted => new(submitted.ReferenceId, 900, 9, DateTime.UtcNow.AddMinutes(15), "{}");
        public HotelInquiryResult? Inquiry { get; set; }
        public Task<HotelInquiryResult> InquireAsync(string referenceId, CancellationToken ct) => Task.FromResult(Inquiry ?? new(HotelInquiryState.Unsupported));
        public HotelProviderCapabilities Capabilities => new(true, Purchase, Inquiry is not null, false, false);
        public Task<IReadOnlyList<string>> ResolveCityIdsAsync(int referenceCityId, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if (MappingFailure is not null) throw MappingFailure; return Task.FromResult<IReadOnlyList<string>>(LegacyId == referenceCityId ? NativeIds ?? [CityId] : []); }
        public IReadOnlyList<string>? NativeIds { get; set; }
        public List<string> SearchedCities { get; } = [];
        public async Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string cityId, HotelTravel travel, CancellationToken ct)
        {
            SearchedCities.Add(cityId); SearchCalls++; if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            if (Failure is not null) throw Failure; if (cityId == FailedCity) throw new HttpRequestException(); return Offers;
        }
        public Task<ProviderHotel> DetailsAsync(string id, string cityId, CancellationToken ct) { DetailsCalls++; return Task.FromResult(Offers.Single(x => x.Hotel.Id == id).Hotel); }
        public Func<HotelSubmitRequest, CancellationToken, Task<HotelSubmitResult>>? OnSubmitAsync { get; set; }
        public async Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct)
        { SubmitCalls++; return OnSubmitAsync is null ? OnSubmit(request) : await OnSubmitAsync(request, ct); }
        public int ConfirmCalls { get; private set; }
        public Func<string, HotelConfirmResult> OnConfirm { get; set; } = _ => new("reservation-final", "Confirmed");
        public Func<string, Task<HotelConfirmResult>>? OnConfirmAsync { get; set; }
        public async Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct) { ConfirmCalls++; return OnConfirmAsync is null ? OnConfirm(referenceId) : await OnConfirmAsync(referenceId); }
    }

    private sealed class PaidMediator(MemoryRequests requests, List<Refahi.Modules.References.Application.Contracts.Dtos.CityDto>? referenceCities = null) : MediatR.IMediator
    {
        public string Payment { get; set; } = "Paid";
        public Refahi.Modules.Orders.Application.Contracts.Dtos.OrderDto? OrphanOrder { get; set; }
        public List<string> RefundKeys { get; } = [];
        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var city = new Refahi.Modules.References.Application.Contracts.Dtos.CityDto(1, "کیش", "Kish", "kish", 1, "هرمزگان", 0, true);
            if (query is Refahi.Modules.References.Application.Contracts.Queries.GetCityByIdQuery getCity)
                return Task.FromResult((TResponse)(object)(referenceCities ?? [city]).FirstOrDefault(x => x.Id == getCity.Id)!);
            if (query is Refahi.Modules.References.Application.Contracts.Queries.GetCitiesQuery)
                return Task.FromResult((TResponse)(object)new Refahi.Modules.References.Application.Contracts.Queries.GetCitiesResponse(referenceCities ?? [city]));
            if (query is Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderByIdempotencyKeyQuery)
                return Task.FromResult((TResponse)(object)OrphanOrder!);
            if (query is Refahi.Modules.Orders.Application.Contracts.Commands.CancelOrderCommand cancel)
            {
                RefundKeys.Add(cancel.IdempotencyKey); Payment = "Refunded";
                return Task.FromResult((TResponse)(object)new Refahi.Modules.Orders.Application.Contracts.Commands.CancelOrderResponse(cancel.OrderId, "Cancelled", "Refunded"));
            }
            if (query is not Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderByIdQuery) throw new NotSupportedException("No refunds or new orders expected.");
            var request = requests.Rows.Single();
            var order = new Refahi.Modules.Orders.Application.Contracts.Dtos.OrderDto(request.OrderId!.Value, "TEST", request.UserId,
                request.TotalPrice, 0, 0, null, 0, request.TotalPrice, "Processing", Payment, "Hotel", request.Id, "HotelRequest", null, null, null, 0, [], DateTimeOffset.UtcNow);
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
        public List<HotelExternalOperation> Operations { get; } = [];
        public Task<IReadOnlyList<HotelExternalOperation>> OperationsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelExternalOperation>>(Operations.Where(x => x.HotelRequestId == id).ToList());
        public void AddOperation(HotelExternalOperation operation) => Operations.Add(operation);
        public Task<IReadOnlyList<HotelCityReference>> CitiesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelCityReference>>(Cities);
        public Task<HotelListing> ListingAsync(int city, string provider, string native, string nativeCity, CancellationToken ct)
        {
            var row = Listings.FirstOrDefault(x => x.Provider == provider && x.NativeId == native);
            if (row is null) { row = HotelListing.Create(city, provider, native, nativeCity); Listings.Add(row); }
            return Task.FromResult(row);
        }
        public Task<HotelListing?> FindListingAsync(Guid key, CancellationToken ct) => Task.FromResult(Listings.FirstOrDefault(x => x.HotelKey == key));
        public Task<HotelOfferSnapshot?> OfferAsync(string token, CancellationToken ct) => Task.FromResult(Offers.FirstOrDefault(x => x.Token == token));
        public Task<HotelExternalReservation?> ReservationAsync(Guid id, CancellationToken ct) => Task.FromResult(Reservations.SingleOrDefault(x => x.HotelRequestId == id));
        public Task<IReadOnlyList<HotelExternalReservation>> RecoveryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HotelExternalReservation>>(Reservations.Where(x => x.NextRecoveryAtUtc <= DateTime.UtcNow).ToList());
        public void AddCity(HotelCityReference row) => Cities.Add(row);
        public void AddOffer(HotelOfferSnapshot row) => Offers.Add(row);
        public void AddReservation(HotelExternalReservation row) => Reservations.Add(row);
        public int SaveCalls { get; private set; }
        public int LockCalls { get; private set; }
        public Task SaveAsync(CancellationToken ct) { SaveCalls++; return Task.CompletedTask; }
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> locks = new();
        public async Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct)
        {
            LockCalls++;
            var semaphore = locks.GetOrAdd(key, _ => new(1)); await semaphore.WaitAsync(ct); return new Lease(semaphore);
        }
        private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
        { public ValueTask DisposeAsync() { semaphore.Release(); return ValueTask.CompletedTask; } }
    }
}
