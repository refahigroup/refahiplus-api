using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;

namespace Refahi.Modules.Flights.Application.Features.Bookings.CreateBooking;

public sealed record CreateFlightBookingCommand(
    Guid UserId,
    string OfferToken,
    FlightBookingContactInput Contact,
    IReadOnlyCollection<FlightBookingPassengerInput> Passengers,
    string IdempotencyKey) : IRequest<FlightBookingDetailDto>;

public sealed record FlightBookingContactInput(
    string MobileNumber,
    string Email);

public sealed record FlightBookingPassengerInput(
    string FirstName,
    string LastName,
    string Gender,
    string PassengerType,
    DateOnly BirthDate,
    string NationalityCode,
    string? NationalCode,
    FlightBookingPassportInput? Passport);

public sealed record FlightBookingPassportInput(
    string? CountryCode,
    DateOnly? IssueDate,
    DateOnly? ExpireDate,
    string? Number);
