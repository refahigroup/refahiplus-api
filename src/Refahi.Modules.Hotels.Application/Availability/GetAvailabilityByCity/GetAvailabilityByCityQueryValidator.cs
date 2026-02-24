using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

namespace Refahi.Modules.Hotels.Application.Availability.GetAvailabilityByCity;

/// <summary>
/// Validator برای GetAvailabilityByCityQuery
/// تمام قوانین تجاری مربوط به جستجو هتل‌های یک شهر را بررسی می‌کند
/// </summary>
public sealed class GetAvailabilityByCityQueryValidator : AbstractValidator<GetAvailabilityByCityQuery>
{
    public GetAvailabilityByCityQueryValidator()
    {
        // ---------------------------------------------------------
        // 1. CityId: باید معتبر و بزرگتر از صفر باشد
        // ---------------------------------------------------------
        RuleFor(x => x.CityId)
            .GreaterThan(0)
            .WithMessage("شناسه شهر نباید خالی باشد.");

        // ---------------------------------------------------------
        // 2. CheckIn: نباید در گذشته باشد
        // ---------------------------------------------------------
        RuleFor(x => x.CheckIn)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("تاریخ ورود نباید در گذشته باشد.");

        // ---------------------------------------------------------
        // 3. CheckOut: نباید قبل یا برابر CheckIn باشد
        // ---------------------------------------------------------
        RuleFor(x => x.CheckOut)
            .GreaterThan(x => x.CheckIn)
            .WithMessage("تاریخ خروج باید بعد از تاریخ ورود باشد.");

        // ---------------------------------------------------------
        // 4. تعداد شب‌ها: حداکثر 90 شب
        // ---------------------------------------------------------
        RuleFor(x => x.CheckOut.DayNumber - x.CheckIn.DayNumber)
            .LessThanOrEqualTo(90)
            .WithMessage("مدت اقامت نمی‌تواند بیش از 90 شب باشد.")
            .When(x => x.CheckOut > x.CheckIn);

        // ---------------------------------------------------------
        // 5. MinPrice و MaxPrice: اگر هر دو تعریف شدند، MinPrice <= MaxPrice
        // ---------------------------------------------------------
        RuleFor(x => x)
            .Custom((query, context) =>
            {
                if (query.MinPrice.HasValue && query.MaxPrice.HasValue)
                {
                    if (query.MinPrice > query.MaxPrice)
                    {
                        context.AddFailure(nameof(query.MinPrice), "حداقل قیمت نباید بیش از حداکثر قیمت باشد.");
                    }
                }
            });

        // ---------------------------------------------------------
        // 6. Adults و Children: اگر تعریف شدند، باید مثبت باشند
        // ---------------------------------------------------------
        RuleFor(x => x.Adults)
            .GreaterThanOrEqualTo(0)
            .WithMessage("تعداد بزرگسالان نمی‌تواند منفی باشد.")
            .When(x => x.Adults.HasValue);

        RuleFor(x => x.Children)
            .GreaterThanOrEqualTo(0)
            .WithMessage("تعداد کودکان نمی‌تواند منفی باشد.")
            .When(x => x.Children.HasValue);

        // ---------------------------------------------------------
        // 7. AvailableRooms: اگر تعریف شده، حداقل 1 باشد
        // ---------------------------------------------------------
        RuleFor(x => x.AvailableRooms)
            .GreaterThanOrEqualTo(1)
            .WithMessage("تعداد اتاق‌های مورد نظر باید حداقل 1 باشد.")
            .When(x => x.AvailableRooms.HasValue);

        // ---------------------------------------------------------
        // 8. Stars: تعداد ستاره‌ها باید بین 1 تا 5 باشد
        // ---------------------------------------------------------
        RuleFor(x => x.Stars)
            .Custom((stars, context) =>
            {
                if (stars != null && stars.Length > 0)
                {
                    if (stars.Any(s => s < 1 || s > 5))
                    {
                        context.AddFailure(nameof(stars), "رتبه‌های ستاره‌ای باید بین 1 تا 5 باشند.");
                    }
                }
            });
    }
}
