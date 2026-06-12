using FluentValidation;

namespace Refahi.Modules.Flights.Application.Features.Search;

public sealed class SearchFlightsQueryValidator : AbstractValidator<SearchFlightsQuery>
{
    public SearchFlightsQueryValidator()
    {
        RuleFor(query => query.Origin)
            .NotEmpty()
            .WithMessage("مبدأ پرواز الزامی است.")
            .Length(3)
            .WithMessage("کد فرودگاه مبدأ باید سه حرفی باشد.");

        RuleFor(query => query.Destination)
            .NotEmpty()
            .WithMessage("مقصد پرواز الزامی است.")
            .Length(3)
            .WithMessage("کد فرودگاه مقصد باید سه حرفی باشد.");

        RuleFor(query => query)
            .Must(query => !string.Equals(query.Origin, query.Destination, StringComparison.OrdinalIgnoreCase))
            .WithMessage("مبدأ و مقصد پرواز نمی‌توانند یکسان باشند.");

        RuleFor(query => query.DepartureDate)
            .NotNull()
            .WithMessage("تاریخ رفت الزامی است.")
            .Must(date => !date.HasValue || date.Value >= DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("تاریخ رفت نمی‌تواند در گذشته باشد.");

        RuleFor(query => query.ReturnDate)
            .Must((query, returnDate) => !returnDate.HasValue || !query.DepartureDate.HasValue || returnDate.Value > query.DepartureDate.Value)
            .WithMessage("تاریخ برگشت باید بعد از تاریخ رفت باشد.");

        RuleFor(query => query.Adult)
            .InclusiveBetween(1, 9)
            .WithMessage("تعداد بزرگسال باید بین ۱ تا ۹ باشد.");

        RuleFor(query => query.Child)
            .InclusiveBetween(0, 9)
            .WithMessage("تعداد کودک باید بین ۰ تا ۹ باشد.");

        RuleFor(query => query.Infant)
            .InclusiveBetween(0, 9)
            .WithMessage("تعداد نوزاد باید بین ۰ تا ۹ باشد.");

        RuleFor(query => query)
            .Must(query => query.Infant <= query.Adult)
            .WithMessage("تعداد نوزاد نمی‌تواند بیشتر از تعداد بزرگسال باشد.");

        RuleFor(query => query.CabinType)
            .NotEmpty()
            .WithMessage("کلاس کابین الزامی است.")
            .MaximumLength(50)
            .WithMessage("کلاس کابین معتبر نیست.");

        RuleFor(query => query.MaxStopsQuantity)
            .GreaterThanOrEqualTo(0)
            .When(query => query.MaxStopsQuantity.HasValue)
            .WithMessage("تعداد توقف معتبر نیست.");
    }
}
