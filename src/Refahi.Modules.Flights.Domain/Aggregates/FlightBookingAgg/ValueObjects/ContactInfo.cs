using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class ContactInfo : ValueObject
{
    private ContactInfo()
    {
        MobileNumber = string.Empty;
    }

    public ContactInfo(string mobileNumber, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            throw new DomainException("Contact mobile number is required.");
        }

        MobileNumber = mobileNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public string MobileNumber { get; private set; }
    public string? Email { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MobileNumber;
        yield return Email;
    }
}
