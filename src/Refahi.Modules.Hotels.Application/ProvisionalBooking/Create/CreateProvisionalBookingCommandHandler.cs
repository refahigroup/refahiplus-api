using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Services.ProvisionalBooking;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Hotels.Application.ProvisionalBooking.Create;

public sealed class CreateProvisionalBookingCommandHandler: IRequestHandler<CreateProvisionalBookingCommand, ProvisionalBookingResponse>
{
    private readonly IHotelProvider _provider;
    private readonly IBookingRepository _repository;
    private readonly ICacheService _cache;

    public CreateProvisionalBookingCommandHandler(
        IHotelProvider provider,
        IBookingRepository repository,
        ICacheService cache)
    {
        _provider = provider;
        _repository = repository;
        _cache = cache;
    }

    public async Task<ProvisionalBookingResponse> Handle(
        CreateProvisionalBookingCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Create provider draft DTO
        var draft = new BookingDraftDto
        {
            HotelId = request.HotelId,
            RoomId = request.RoomId,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            RoomsCount = request.RoomsCount,
            Guests = request.Guests,
            BoardType = request.BoardType
        };

        // 2. provider create booking
        var providerResult = await _provider.CreateBookingAsync(draft);

        // 3. convert DTO to Domain types
        var bookingId = BookingId.New();
        var now = DateTime.UtcNow;

        var guests = request.Guests.Select(g => new Guest(
            g.FullName,
            g.Age,
            Enum.Parse<GuestType>(g.Type, true)
        ));

        var domainstay = new DateRange(request.CheckIn, request.CheckOut);

        var domain = Booking.CreateProvisional(
            bookingId,
            ProviderType.SnappTrip,
            new ProviderBookingCode(providerResult.ProviderBookingCode),
            new ProviderHotelId(request.HotelId),
            new ProviderRoomId(request.RoomId),
            domainstay,
            guests,
            request.RoomsCount,
            Enum.Parse<RoomBoardType>(request.BoardType, true),
            new Money(providerResult.ProviderPrice),
            new Money(0), // margin فعلا 0 — بعداً PricingService اضافه می‌کنیم
            new Money(providerResult.ProviderPrice),
            now,
            providerResult.LockedUntil
        );

        // 4. save booking
        await _repository.AddAsync(domain);
        await _repository.SaveChangesAsync();

        // 5. cache expiration (optional)
        if (domain.LockedUntil.HasValue)
        {
            await _cache.SetAsync(
                key: $"booking:lock:{domain.Id}",
                value: domain.LockedUntil.Value,
                ttl: TimeSpan.FromMinutes(20)
            );
        }

        // 6. return
        return new ProvisionalBookingResponse
        {
            BookingId = domain.Id.Value,
            CustomerPrice = providerResult.ProviderPrice,
            ExpiresAt = providerResult.LockedUntil
        };
    }
}
