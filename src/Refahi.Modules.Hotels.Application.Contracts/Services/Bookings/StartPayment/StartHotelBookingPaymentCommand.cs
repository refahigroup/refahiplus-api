using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.StartPayment;

public sealed record StartHotelBookingPaymentCommand(Guid BookingId) : IRequest<StartHotelBookingPaymentResponse>;

