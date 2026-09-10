using Refahi.Modules.Flights.Application.Features.Search;
using Refahi.Modules.Flights.Application.Features.Bookings.CreateBooking;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightValidationTests
{
    [Theory]
    [InlineData("IR")]
    [InlineData(" ir ")]
    [InlineData("IRN")]
    public void CreateBookingValidator_RequiresNationalCodeForIranianPassenger(
        string nationalityCode
    )
    {
        var validator = new CreateFlightBookingCommandValidator();
        var command = CreateBookingCommand(
            nationalityCode,
            nationalCode: null,
            passport: new FlightBookingPassportInput(
                "IR",
                new DateOnly(2020, 1, 1),
                new DateOnly(2030, 1, 1),
                "A12345678"
            )
        );

        var result = validator.Validate(command);

        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "کد ملی برای مسافر ایرانی الزامی است."
        );
    }

    [Fact]
    public void CreateBookingValidator_AcceptsIranianPassengerWithNationalCodeAndPassport()
    {
        var validator = new CreateFlightBookingCommandValidator();
        var command = CreateBookingCommand(
            "IR",
            nationalCode: "0154721621",
            passport: new FlightBookingPassportInput(
                "IR",
                new DateOnly(2020, 1, 1),
                new DateOnly(2030, 1, 1),
                "A12345678"
            )
        );

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateBookingValidator_RequiresPassportForNonIranianPassenger()
    {
        var validator = new CreateFlightBookingCommandValidator();
        var command = CreateBookingCommand("DE", nationalCode: null, passport: null);

        var result = validator.Validate(command);

        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage == "شماره گذرنامه برای مسافر غیرایرانی الزامی است."
        );
    }

    [Fact]
    public void SearchFlightsQueryValidator_AcceptsPassengerUpperBoundary()
    {
        var validator = new SearchFlightsQueryValidator();
        var query = CreateValidQuery(adult: 20, child: 20, infant: 20);

        var result = validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SearchFlightsQueryValidator_RejectsPassengerValuesAboveTwenty()
    {
        var validator = new SearchFlightsQueryValidator();
        var result = validator.Validate(CreateValidQuery(adult: 21, child: 21, infant: 21));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(SearchFlightsQuery.Adult)
        );
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(SearchFlightsQuery.Child)
        );
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(SearchFlightsQuery.Infant)
        );
    }

    [Fact]
    public void SearchFlightsQueryValidator_ReturnsPersianErrors()
    {
        var validator = new SearchFlightsQueryValidator();
        var query = new SearchFlightsQuery(
            Origin: "",
            Destination: "",
            DepartureDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)),
            ReturnDate: null,
            Adult: 0,
            Child: 0,
            Infant: 1,
            CabinType: "",
            AirTripType: null,
            IsDomestic: true,
            MaxStopsQuantity: -1,
            VendorExcludeCodes: null,
            VendorPreferenceCodes: null
        );

        var result = validator.Validate(query);
        var messages = result.Errors.Select(error => error.ErrorMessage).ToArray();

        Assert.False(result.IsValid);
        Assert.Contains("مبدأ پرواز الزامی است.", messages);
        Assert.Contains("مقصد پرواز الزامی است.", messages);
        Assert.Contains("تاریخ رفت نمی‌تواند در گذشته باشد.", messages);
        Assert.Contains("تعداد بزرگسال باید بین ۱ تا ۲۰ باشد.", messages);
        Assert.Contains("تعداد نوزاد نمی‌تواند بیشتر از تعداد بزرگسال باشد.", messages);
        Assert.Contains("کلاس کابین الزامی است.", messages);
        Assert.Contains("تعداد توقف معتبر نیست.", messages);
    }

    private static SearchFlightsQuery CreateValidQuery(int adult, int child, int infant) =>
        new(
            Origin: "THR",
            Destination: "MHD",
            DepartureDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
            ReturnDate: null,
            Adult: adult,
            Child: child,
            Infant: infant,
            CabinType: "Economy",
            AirTripType: "OneWay",
            IsDomestic: true,
            MaxStopsQuantity: null,
            VendorExcludeCodes: null,
            VendorPreferenceCodes: null
        );

    private static CreateFlightBookingCommand CreateBookingCommand(
        string nationalityCode,
        string? nationalCode,
        FlightBookingPassportInput? passport
    ) =>
        new(
            Guid.NewGuid(),
            "offer-token",
            new FlightBookingContactInput("09123456789", "passenger@example.com"),
            [
                new FlightBookingPassengerInput(
                    "Ali",
                    "Karimi",
                    "Male",
                    "Adult",
                    new DateOnly(1990, 4, 1),
                    nationalityCode,
                    nationalCode,
                    passport
                ),
            ],
            "idempotency-key"
        );
}
