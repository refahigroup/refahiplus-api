using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Cart.AddItem;

public sealed class AddCommerceCartItemValidator : AbstractValidator<AddCommerceCartItemCommand>
{
    public AddCommerceCartItemValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.ProviderKey)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("تامین‌کننده معتبر نیست");

        RuleFor(x => x.SellerKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("فروشنده معتبر نیست");

        RuleFor(x => x.ProductKey)
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
