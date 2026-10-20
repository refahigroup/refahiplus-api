using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;

namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelTravelValidator : AbstractValidator<HotelTravel>
{
    public HotelTravelValidator() => RuleFor(x => x).Custom((travel, context) =>
    {
        try { HotelCatalogueService.ValidateTravel(travel); }
        catch (ValidationException) { context.AddFailure("شرایط اقامت و مسافران معتبر نیست."); }
    });
}
public sealed class SearchHotelCatalogueValidator : AbstractValidator<SearchHotelCatalogueQuery>
{
    public SearchHotelCatalogueValidator()
    {
        RuleFor(x => x).Must(x => x.CityKey.HasValue && x.CityKey != Guid.Empty || x.LegacyCityId > 0).WithMessage("شهر مقصد الزامی است.");
        RuleFor(x => x.Travel).NotNull().WithMessage("شرایط اقامت الزامی است.").SetValidator(new HotelTravelValidator());
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).WithMessage("حداقل قیمت معتبر نیست.");
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).WithMessage("حداکثر قیمت معتبر نیست.");
        RuleFor(x => x).Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MaxPrice >= x.MinPrice).WithMessage("بازه قیمت معتبر نیست.");
        RuleFor(x => x.Stars).Must(x => x is null || x.Length <= 6 && x.All(s => s is >= 0 and <= 5)).WithMessage("درجه هتل معتبر نیست.");
        RuleFor(x => x.Accommodations).Must(x => x is null || x.Length <= 10 && x.All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 64)).WithMessage("نوع اقامتگاه معتبر نیست.");
        RuleFor(x => x.Sort).Must(x => x is "price" or "price-desc" or "stars").WithMessage("ترتیب نتایج معتبر نیست.");
    }
}
public sealed class HotelDetailsCatalogueValidator : AbstractValidator<GetHotelCatalogueDetailsQuery>
{
    public HotelDetailsCatalogueValidator()
    {
        RuleFor(x => x.HotelKey).NotEmpty().WithMessage("هتل معتبر نیست.");
        RuleFor(x => x.Travel).NotNull().WithMessage("شرایط اقامت الزامی است.").SetValidator(new HotelTravelValidator());
    }
}
public sealed class HotelQuoteCatalogueValidator : AbstractValidator<QuoteHotelOfferQuery>
{
    public HotelQuoteCatalogueValidator()
    {
        RuleFor(x => x.OfferToken).NotEmpty().MaximumLength(64).WithMessage("پیشنهاد معتبر نیست.");
        RuleFor(x => x.Travel).NotNull().WithMessage("شرایط اقامت الزامی است.").SetValidator(new HotelTravelValidator());
    }
}
public sealed class HotelOfferCatalogueValidator : AbstractValidator<GetHotelOfferQuery>
{
    public HotelOfferCatalogueValidator() => RuleFor(x => x.OfferToken).NotEmpty().MaximumLength(64).WithMessage("پیشنهاد معتبر نیست.");
}
public sealed class HotelCitiesCatalogueValidator : AbstractValidator<GetHotelCitiesQuery>
{
    public HotelCitiesCatalogueValidator() => RuleFor(x => x.Name).MaximumLength(100).WithMessage("نام شهر معتبر نیست.");
}
