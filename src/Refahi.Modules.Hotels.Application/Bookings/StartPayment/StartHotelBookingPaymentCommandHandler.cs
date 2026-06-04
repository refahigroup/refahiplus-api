using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.StartPayment;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Application.Bookings.StartPayment;

public sealed class StartHotelBookingPaymentCommandHandler
    : IRequestHandler<StartHotelBookingPaymentCommand, StartHotelBookingPaymentResponse>
{
    private readonly IBookingRepository _repository;

    public StartHotelBookingPaymentCommandHandler(IBookingRepository repository)
        => _repository = repository;

    public async Task<StartHotelBookingPaymentResponse> Handle(StartHotelBookingPaymentCommand request, CancellationToken cancellationToken)
    {
        var booking = await _repository.GetAsync(new BookingId(request.BookingId), cancellationToken)
            ?? throw new InvalidOperationException("Booking not found.");

        booking.MarkPaymentPending(DateTime.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return new StartHotelBookingPaymentResponse
        {
            BookingId = booking.Id.Value,
            AmountMinor = booking.CustomerPrice.Amount
        };
    }
}

