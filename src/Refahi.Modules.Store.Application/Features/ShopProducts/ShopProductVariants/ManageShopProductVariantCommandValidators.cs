using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

public class EnableShopProductVariantCommandValidator : AbstractValidator<EnableShopProductVariantCommand>
{
    public EnableShopProductVariantCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("شناسه تنوع محصول الزامی است");
    }
}

public class DisableShopProductVariantCommandValidator : AbstractValidator<DisableShopProductVariantCommand>
{
    public DisableShopProductVariantCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("شناسه تنوع محصول الزامی است");
    }
}

public class RemoveShopProductVariantCommandValidator : AbstractValidator<RemoveShopProductVariantCommand>
{
    public RemoveShopProductVariantCommandValidator()
    {
        RuleFor(x => x.ShopId)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("شناسه تنوع محصول الزامی است");
    }
}
