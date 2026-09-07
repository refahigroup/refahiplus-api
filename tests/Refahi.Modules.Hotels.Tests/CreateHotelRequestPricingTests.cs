using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Account;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Hotel;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;
using Refahi.Modules.Hotels.Application.HotelRequests.CreateHotelRequest;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class CreateHotelRequestPricingTests
{
    [Fact]
    public async Task Handle_WhenExpectedPriceDiffers_ReturnsPriceChangeWithoutMutation()
    {
        var requests = new RequestRepository();
        var sagas = new SagaRepository();
        var provider = new QuoteProvider(12_000_000);
        var handler = CreateHandler(requests, sagas, provider);

        var exception = await Assert.ThrowsAsync<HotelPriceChangedException>(() =>
            handler.Handle(CreateCommand(11_000_000), CancellationToken.None)
        );

        Assert.Equal(12_000_000, exception.CurrentPriceMinor);
        Assert.Empty(requests.Items);
        Assert.Empty(sagas.Items);
    }

    [Fact]
    public async Task Handle_WhenQuoteMatches_PersistsOnlyServerPriceAndPricingVersionTwo()
    {
        var requests = new RequestRepository();
        var sagas = new SagaRepository();
        var provider = new QuoteProvider(12_000_000);
        var handler = CreateHandler(requests, sagas, provider);

        var response = await handler.Handle(CreateCommand(12_000_000), CancellationToken.None);

        var saved = Assert.Single(requests.Items);
        Assert.Equal(12_000_000, response.TotalPrice);
        Assert.Equal(12_000_000, saved.TotalPrice);
        Assert.Equal("IRR", saved.Currency);
        Assert.Equal(HotelRequest.CurrentPricingVersion, saved.PricingVersion);
        Assert.Contains("\"discountAmountMinor\":0", saved.Breakdown);
        Assert.Single(sagas.Items);
    }

    [Fact]
    public async Task Handle_IdempotencyReplay_DoesNotRequestAnotherQuote()
    {
        var requests = new RequestRepository();
        var sagas = new SagaRepository();
        var provider = new QuoteProvider(12_000_000);
        var handler = CreateHandler(requests, sagas, provider);
        var command = CreateCommand(12_000_000);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.RequestId, second.RequestId);
        Assert.Equal(1, provider.QuoteCount);
        Assert.Single(requests.Items);
    }

    private static CreateHotelRequestCommandHandler CreateHandler(
        RequestRepository requests,
        SagaRepository sagas,
        QuoteProvider provider
    ) =>
        new(
            requests,
            sagas,
            new ProviderFactory(provider),
            NullLogger<CreateHotelRequestCommandHandler>.Instance
        );

    private static CreateHotelRequestCommand CreateCommand(long expectedPrice) =>
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "SnappTrip",
            10,
            20,
            30,
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 10),
            2,
            1,
            1,
            "BedBreakfast",
            expectedPrice,
            "{\"totalPrice\":1}",
            "{\"name\":\"hotel\"}",
            "{\"price\":1}",
            null,
            "{\"guest\":\"test\"}",
            "pricing-test-key"
        );

    private sealed class ProviderFactory(IHotelProvider provider) : IHotelProviderFactory
    {
        public IHotelProvider GetProvider(HotelProviderType providerType) => provider;
        public IHotelProvider GetDefaultProvider() => provider;
    }

    private sealed class QuoteProvider(long price) : IHotelProvider
    {
        public int QuoteCount { get; private set; }

        public Task<HotelRoomPriceQuoteDto> QuoteRoomPriceAsync(
            HotelRoomPriceQuoteRequest request,
            CancellationToken cancellationToken = default
        )
        {
            QuoteCount++;
            return Task.FromResult(
                new HotelRoomPriceQuoteDto(request.HotelId, request.RoomId, price, "IRR")
            );
        }

        public Task<IEnumerable<GetCitiesResponse>> GetAllCities(string? name) => throw new NotSupportedException();
        public Task<GetAvailabilityByCityDto> GetAvailabilityByCity(GetAvailabilityByCityQuery query) => throw new NotSupportedException();
        public Task<IEnumerable<HotelDetailsDto>> GetHotelDetailsAsync(GetHotelDetailsQuery query) => throw new NotSupportedException();
        public Task<AvailabilityCalendarDto> GetHotelAvailabilityCalendarAsync(long hotelId, DateOnly from, DateOnly to) => throw new NotSupportedException();
        public Task<HotelReviewsDto> GetHotelReviewsAsync(long hotelId, int page = 1, int pageSize = 10) => throw new NotSupportedException();
        public Task<AccountBalanceDto> GetAccountBalanceAsync() => throw new NotSupportedException();
        public Task<BookingCreateResultDto> CreateBookingAsync(BookingDraftDto dto) => throw new NotSupportedException();
        public Task LockBookingAsync(string bookingCode) => throw new NotSupportedException();
        public Task ConfirmBookingAsync(string bookingCode) => throw new NotSupportedException();
        public Task<BookingStatusDto> GetBookingStatusAsync(string bookingCode) => throw new NotSupportedException();
        public Task<CancelProviderBookingResultDto> CancelBookingAsync(string bookingCode, string idempotencyKey, string reason) => throw new NotSupportedException();
    }

    private sealed class RequestRepository : IHotelRequestRepository
    {
        public List<HotelRequest> Items { get; } = [];
        public Task<HotelRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<HotelRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id && x.UserId == userId));
        public Task<HotelRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.OrderId == orderId));
        public Task<HotelRequest?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey));
        public Task AddAsync(HotelRequest request, CancellationToken cancellationToken = default) { Items.Add(request); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SagaRepository : IHotelBookingSagaRepository
    {
        public List<HotelBookingSagaState> Items { get; } = [];
        public Task<HotelBookingSagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.SagaId == sagaId));
        public Task<HotelBookingSagaState?> GetByHotelRequestIdAsync(Guid hotelRequestId, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.HotelRequestId == hotelRequestId));
        public Task<HotelBookingSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(x => x.OrderId == orderId));
        public Task<IReadOnlyList<HotelBookingSagaState>> GetStuckAsync(IReadOnlyCollection<HotelBookingSagaStatus> statuses, DateTime olderThanUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HotelBookingSagaState>>([]);
        public Task AddAsync(HotelBookingSagaState saga, CancellationToken cancellationToken = default) { Items.Add(saga); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
