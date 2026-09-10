using FluentValidation;

namespace Refahi.Modules.Flights.Application.Features.Bookings.CreateBooking;

public sealed class CreateFlightBookingCommandValidator
    : AbstractValidator<CreateFlightBookingCommand>
{
    public CreateFlightBookingCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است.");

        RuleFor(command => command.OfferToken)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("توکن پیشنهاد پرواز الزامی است.");

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("کلید یکتاسازی الزامی است.");

        RuleFor(command => command.Contact).NotNull().WithMessage("اطلاعات تماس الزامی است.");

        RuleFor(command => command.Contact.MobileNumber)
            .NotEmpty()
            .MaximumLength(30)
            .When(command => command.Contact is not null)
            .WithMessage("شماره موبایل مسافر الزامی است.");

        RuleFor(command => command.Contact.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .When(command => command.Contact is not null)
            .WithMessage("ایمیل معتبر الزامی است.");

        RuleFor(command => command.Passengers).NotEmpty().WithMessage("حداقل یک مسافر الزامی است.");

        RuleForEach(command => command.Passengers)
            .SetValidator(new FlightBookingPassengerInputValidator());
    }
}

internal sealed class FlightBookingPassengerInputValidator
    : AbstractValidator<FlightBookingPassengerInput>
{
    public FlightBookingPassengerInputValidator()
    {
        RuleFor(passenger => passenger.FirstName)
            .NotEmpty()
            .MaximumLength(150)
            .WithMessage("نام مسافر الزامی است.");

        RuleFor(passenger => passenger.LastName)
            .NotEmpty()
            .MaximumLength(150)
            .WithMessage("نام خانوادگی مسافر الزامی است.");

        RuleFor(passenger => passenger.Gender)
            .NotEmpty()
            .Must(value =>
                value.Equals("Male", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Female", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("جنسیت مسافر معتبر نیست.");

        RuleFor(passenger => passenger.PassengerType)
            .NotEmpty()
            .Must(value =>
                value.Equals("Adult", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Child", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Infant", StringComparison.OrdinalIgnoreCase)
            )
            .WithMessage("نوع مسافر معتبر نیست.");

        RuleFor(passenger => passenger.BirthDate)
            .NotEmpty()
            .WithMessage("تاریخ تولد مسافر الزامی است.");

        RuleFor(passenger => passenger.NationalityCode)
            .NotEmpty()
            .Length(2, 3)
            .WithMessage("کد ملیت مسافر معتبر نیست.");

        RuleFor(passenger => passenger.NationalCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("کد ملی برای مسافر ایرانی الزامی است.")
            .Matches("^[0-9]{10}$")
            .WithMessage("کد ملی مسافر باید ۱۰ رقم باشد.")
            .When(passenger => IsIranianNationality(passenger.NationalityCode));

        RuleFor(passenger => passenger.Passport)
            .Must(passport => !string.IsNullOrWhiteSpace(passport?.Number))
            .WithMessage("شماره گذرنامه برای مسافر غیرایرانی الزامی است.")
            .When(passenger => !IsIranianNationality(passenger.NationalityCode));
    }

    private static bool IsIranianNationality(string? nationalityCode)
    {
        var normalized = nationalityCode?.Trim();
        return string.Equals(normalized, "IR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "IRN", StringComparison.OrdinalIgnoreCase);
    }
}
