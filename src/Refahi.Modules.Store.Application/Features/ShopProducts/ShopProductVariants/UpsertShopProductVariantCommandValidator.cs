using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

public class UpsertShopProductVariantCommandValidator : AbstractValidator<UpsertShopProductVariantCommand>
{
    public UpsertShopProductVariantCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("شناسه تنوع محصول الزامی است");

        RuleFor(x => x.PriceMinor)
            .GreaterThan(0).WithMessage("قیمت باید بیشتر از صفر باشد");

        RuleFor(x => x.DiscountedPriceMinor)
            .NotNull().WithMessage("قیمت تخفیف‌خورده الزامی است")
            .GreaterThan(0).WithMessage("قیمت تخفیف‌خورده باید بیشتر از صفر باشد")
            .LessThanOrEqualTo(x => x.PriceMinor).WithMessage("قیمت تخفیف‌خورده نباید بیشتر از قیمت اصلی باشد");
    }
}
