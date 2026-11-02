using FluentValidation;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;

public sealed class AddEntertainmentCartItemValidator : AbstractValidator<AddEntertainmentCartItemCommand>
{
    public AddEntertainmentCartItemValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.ProviderKey)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("تامین‌کننده معتبر نیست");

        RuleFor(x => x.SupplierKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("فروشنده معتبر نیست");

        RuleFor(x => x.ProgramKey)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("محصول معتبر نیست");

        RuleFor(x => x.OfferKey)
            .NotEmpty().MaximumLength(500)
            .WithMessage("آفر معتبر نیست");

        RuleFor(x => x.PurchaseOptionKey)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("نوع بلیط معتبر نیست");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 100)
            .WithMessage("تعداد باید بین ۱ تا ۱۰۰ باشد");

        RuleFor(x => x.ExpectedUnitPriceMinor)
            .GreaterThan(0)
            .WithMessage("قیمت معتبر نیست");
    }
}
