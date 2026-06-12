using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;

public sealed class Passenger
{
    private Passenger()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        NationalityCode = string.Empty;
    }

    public Passenger(
        string firstName,
        string lastName,
        FlightPassengerType type,
        DateOnly birthDate,
        string? nationalCode,
        string? passportNumber,
        string nationalityCode)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("Passenger first name is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Passenger last name is required.");
        }

        if (string.IsNullOrWhiteSpace(nationalCode) && string.IsNullOrWhiteSpace(passportNumber))
        {
            throw new DomainException("Passenger national code or passport number is required.");
        }

        if (string.IsNullOrWhiteSpace(nationalityCode))
        {
            throw new DomainException("Passenger nationality code is required.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Type = type;
        BirthDate = birthDate;
        NationalCode = string.IsNullOrWhiteSpace(nationalCode) ? null : nationalCode.Trim();
        PassportNumber = string.IsNullOrWhiteSpace(passportNumber) ? null : passportNumber.Trim();
        NationalityCode = nationalityCode.Trim().ToUpperInvariant();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public FlightPassengerType Type { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public string? NationalCode { get; private set; }
    public string? PassportNumber { get; private set; }
    public string NationalityCode { get; private set; }

    public string DisplayName => $"{FirstName} {LastName}";
}
