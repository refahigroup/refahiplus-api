using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariantBackfill;

public class BackfillShopProductVariantsCommandValidator : AbstractValidator<BackfillShopProductVariantsCommand>
{
    public BackfillShopProductVariantsCommandValidator()
    {
        RuleFor(x => x.DetailLimit)
            .GreaterThanOrEqualTo(0).WithMessage("محدودیت جزئیات نباید منفی باشد")
            .LessThanOrEqualTo(500).WithMessage("محدودیت جزئیات نباید بیشتر از ۵۰۰ باشد");
    }
}
