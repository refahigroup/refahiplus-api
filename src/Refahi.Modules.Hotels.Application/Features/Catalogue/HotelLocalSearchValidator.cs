using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;

namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelLocalSearchValidator : AbstractValidator<HotelLocalSearch>
{
    public HotelLocalSearchValidator()
    {
        RuleFor(x => x.Query).MaximumLength(500).WithMessage("متن جست‌وجو بیش از حد طولانی است.");
        RuleFor(x => x.Page).InclusiveBetween(1, 10000).WithMessage("شماره صفحه معتبر نیست.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50).WithMessage("تعداد نتایج معتبر نیست.");
        RuleFor(x => x.Sort).Must(x => x is "title" or "rating" or "distance").WithMessage("ترتیب نتایج معتبر نیست.");
        RuleFor(x => x.CityId).GreaterThan(0).When(x => x.CityId.HasValue).WithMessage("شهر معتبر نیست.");
        RuleFor(x => x.ProvinceId).GreaterThan(0).When(x => x.ProvinceId.HasValue).WithMessage("استان معتبر نیست.");
        RuleForEach(x => x.Stars).InclusiveBetween(0, 5).WithMessage("درجه هتل معتبر نیست.");
        RuleForEach(x => x.Facilities).NotEmpty().MaximumLength(100).WithMessage("امکانات معتبر نیست.");
        RuleForEach(x => x.Accommodations).NotEmpty().MaximumLength(100).WithMessage("نوع اقامتگاه معتبر نیست.");
        RuleFor(x => x).Must(x => x.Facilities?.Length is not > 30 && x.Accommodations?.Length is not > 30 && x.Stars?.Length is not > 6).WithMessage("تعداد فیلترها بیش از حد مجاز است.");
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue && (!x.Latitude.HasValue
            || double.IsFinite(x.Latitude.Value) && double.IsFinite(x.Longitude!.Value) && x.Latitude is >= -90 and <= 90 && x.Longitude is >= -180 and <= 180))
            .WithMessage("مختصات معتبر نیست.");
        RuleFor(x => x).Must(x => x.Sort != "distance" || x.Latitude.HasValue).WithMessage("مرتب‌سازی فاصله به مختصات نیاز دارد.");
        RuleFor(x => x.MinRating).Must(x => !x.HasValue || double.IsFinite(x.Value) && x.Value is >= 0 and <= 10).WithMessage("امتیاز معتبر نیست.");
    }
}
