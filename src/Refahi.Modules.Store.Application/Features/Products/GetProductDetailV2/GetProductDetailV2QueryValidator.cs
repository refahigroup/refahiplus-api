using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductDetailV2;

public sealed class GetProductDetailV2QueryValidator : AbstractValidator<GetProductDetailV2Query>
{
    public GetProductDetailV2QueryValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(500).WithMessage("آدرس محصول معتبر نیست.");
        RuleFor(x => x.ShopId).NotEqual(Guid.Empty).When(x => x.ShopId.HasValue)
            .WithMessage("شناسه فروشگاه معتبر نیست.");
        RuleFor(x => x.VariantId).NotEqual(Guid.Empty).When(x => x.VariantId.HasValue)
            .WithMessage("شناسه تنوع معتبر نیست.");
        RuleFor(x => x.OfferKey).MaximumLength(100).WithMessage("کلید Offer معتبر نیست.");
    }
}

