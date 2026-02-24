using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public sealed class Guest
{
    public string FullName { get; }
    public int Age { get; }
    public GuestType Type { get; }

    public Guest(string fullName, int age, GuestType type)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Guest full name is required.");

        if (age < 0)
            throw new DomainException("Guest age cannot be negative.");

        FullName = fullName.Trim();
        Age = age;
        Type = type;
    }
}
