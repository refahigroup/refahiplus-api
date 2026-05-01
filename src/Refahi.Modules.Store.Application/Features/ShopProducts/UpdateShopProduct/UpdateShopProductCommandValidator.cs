using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.UpdateShopProduct;

public class UpdateShopProductCommandValidator : AbstractValidator<UpdateShopProductCommand>
{
    public UpdateShopProductCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("قیمت باید بزرگتر از صفر باشد");

        RuleFor(x => x.DiscountedPrice)
            .GreaterThan(0).WithMessage("قیمت با تخفیف باید بزرگتر از صفر باشد")
            .LessThanOrEqualTo(x => x.Price).WithMessage("قیمت با تخفیف نباید بیشتر از قیمت اصلی باشد");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("توضیحات نباید بیشتر از ۲۰۰۰ کاراکتر باشد");
    }
}
