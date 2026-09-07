using System.Text.Json;
using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;

namespace Refahi.Modules.Hotels.Application.HotelRequests.CreateHotelRequest;

public sealed class CreateHotelRequestCommandValidator
    : AbstractValidator<CreateHotelRequestCommand>
{
    public CreateHotelRequestCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر الزامی است");
        RuleFor(x => x.ProviderHotelId).GreaterThan(0).WithMessage("شناسه هتل معتبر نیست");
        RuleFor(x => x.ProviderRoomId).GreaterThan(0).WithMessage("شناسه اتاق معتبر نیست");
        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("شناسه شهر معتبر نیست");
        RuleFor(x => x.CheckOut).GreaterThan(x => x.CheckIn).WithMessage("بازه اقامت معتبر نیست");
        RuleFor(x => x.Adults).GreaterThan(0).WithMessage("تعداد بزرگسالان معتبر نیست");
        RuleFor(x => x.Children).GreaterThanOrEqualTo(0).WithMessage("تعداد کودکان معتبر نیست");
        RuleFor(x => x.Rooms).GreaterThan(0).WithMessage("تعداد اتاق‌ها معتبر نیست");
        RuleFor(x => x.BoardType).NotEmpty().WithMessage("نوع پذیرایی اتاق معتبر نیست");
        RuleFor(x => x.ExpectedTotalPriceMinor)
            .GreaterThan(0)
            .WithMessage("مبلغ مورد انتظار رزرو معتبر نیست");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("کلید تکرارپذیری الزامی است");
        RuleFor(x => x.SearchCriteriaSnapshot)
            .Must(BeJson)
            .WithMessage("ساختار جست‌وجو معتبر نیست");
        RuleFor(x => x.SelectedHotelSnapshot).Must(BeJson).WithMessage("ساختار هتل معتبر نیست");
        RuleFor(x => x.SelectedRoomSnapshot).Must(BeJson).WithMessage("ساختار اتاق معتبر نیست");
        RuleFor(x => x.Fees)
            .Must(x => string.IsNullOrWhiteSpace(x) || BeJson(x))
            .WithMessage("ساختار کارمزد معتبر نیست");
        RuleFor(x => x.GuestInfoSnapshot).Must(BeJson).WithMessage("ساختار مسافران معتبر نیست");
    }

    private static bool BeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
