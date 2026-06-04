using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.GetBookingDetails;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Application.Bookings.GetBookingDetails;

public sealed class GetHotelBookingDetailsQueryHandler
    : IRequestHandler<GetHotelBookingDetailsQuery, HotelBookingDetailsResponse?>
{
    private readonly IBookingRepository _repository;

    public GetHotelBookingDetailsQueryHandler(IBookingRepository repository)
        => _repository = repository;

    public async Task<HotelBookingDetailsResponse?> Handle(GetHotelBookingDetailsQuery request, CancellationToken cancellationToken)
    {
        var booking = await _repository.GetAsync(new BookingId(request.BookingId), cancellationToken);
        if (booking is null)
            return null;

        return new HotelBookingDetailsResponse
        {
            BookingId = booking.Id.Value,
            HotelId = booking.ProviderHotelId.Value,
            RoomId = booking.ProviderRoomId.Value,
            CheckIn = booking.StayRange.CheckIn,
            CheckOut = booking.StayRange.CheckOut,
            RoomsCount = booking.RoomsCount,
            BoardType = booking.BoardType.ToString(),
            Status = booking.Status.ToString(),
            CustomerPrice = booking.CustomerPrice.Amount,
            ExpiresAt = booking.LockedUntil,
            Guests = booking.Guests.Select(g => new HotelBookingGuestResponse
            {
                FullName = g.FullName,
                Age = g.Age,
                Type = g.Type.ToString()
            }).ToList(),
            Voucher = booking.Voucher is null
                ? null
                : new HotelBookingVoucherResponse { Code = booking.Voucher.VoucherNumber ?? "" }
        };
    }
}
