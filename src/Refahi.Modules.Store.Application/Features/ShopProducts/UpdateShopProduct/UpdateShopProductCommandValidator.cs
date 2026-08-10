using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.UpdateShopProduct;

public class UpdateShopProductCommandValidator : AbstractValidator<UpdateShopProductCommand>
{
    public UpdateShopProductCommandValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("قیمت نمی‌تواند منفی باشد");

        RuleFor(x => x.DiscountedPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("قیمت با تخفیف نمی‌تواند منفی باشد")
            .LessThanOrEqualTo(x => x.Price)
            .WithMessage("قیمت با تخفیف نباید بیشتر از قیمت اصلی باشد");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("توضیحات نباید بیشتر از ۲۰۰۰ کاراکتر باشد");
    }
}
