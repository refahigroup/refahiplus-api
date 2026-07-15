using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Features.Products.GetProductCatalogV2;

namespace Refahi.Modules.Store.Application.Features.Products.GetSyntheticOffersV2;

public sealed class GetSyntheticOffersV2QueryValidator : AbstractValidator<GetSyntheticOffersV2Query>
{
    private static readonly string[] OfferKinds = ["StockProduct", "StockVariant", "ProductSession", "SessionVariant"];

    public GetSyntheticOffersV2QueryValidator()
    {
        RuleFor(x => x.SearchQuery).MaximumLength(200).WithMessage("عبارت جست‌وجو حداکثر ۲۰۰ کاراکتر است.");
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue)
            .WithMessage("دسته‌بندی معتبر نیست.");
        RuleFor(x => x.ShopId).NotEqual(Guid.Empty).When(x => x.ShopId.HasValue)
            .WithMessage("شناسه فروشگاه معتبر نیست.");
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty).When(x => x.ProductId.HasValue)
            .WithMessage("شناسه محصول معتبر نیست.");
        RuleFor(x => x.ProductSlug).MaximumLength(500).WithMessage("آدرس محصول معتبر نیست.");
        RuleFor(x => x)
            .Must(x => !x.ProductId.HasValue || string.IsNullOrWhiteSpace(x.ProductSlug))
            .WithMessage("فقط یکی از شناسه یا آدرس محصول را ارسال کنید.");
        RuleFor(x => x.SalesModel).Must(GetProductCatalogV2QueryValidator.IsSalesModel)
            .When(x => !string.IsNullOrWhiteSpace(x.SalesModel)).WithMessage("مدل فروش معتبر نیست.");
        RuleFor(x => x.OfferKind).Must(IsOfferKind).When(x => !string.IsNullOrWhiteSpace(x.OfferKind))
            .WithMessage("نوع Offer معتبر نیست.");
        RuleFor(x => x.MinPriceMinor).GreaterThanOrEqualTo(0).When(x => x.MinPriceMinor.HasValue)
            .WithMessage("حداقل قیمت نمی‌تواند منفی باشد.");
        RuleFor(x => x.MaxPriceMinor).GreaterThanOrEqualTo(0).When(x => x.MaxPriceMinor.HasValue)
            .WithMessage("حداکثر قیمت نمی‌تواند منفی باشد.");
        RuleFor(x => x).Must(x => !x.MinPriceMinor.HasValue || !x.MaxPriceMinor.HasValue || x.MinPriceMinor <= x.MaxPriceMinor)
            .WithMessage("حداقل قیمت باید کوچک‌تر یا مساوی حداکثر قیمت باشد.");
        RuleFor(x => x).Must(x => !x.UsageFrom.HasValue || !x.UsageTo.HasValue || x.UsageFrom <= x.UsageTo)
            .WithMessage("تاریخ شروع استفاده باید قبل از تاریخ پایان باشد.");
        RuleFor(x => x.Sort).Must(GetProductCatalogV2QueryValidator.IsSort).WithMessage("مرتب‌سازی معتبر نیست.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("شماره صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 30).WithMessage("اندازه صفحه باید بین ۱ تا ۳۰ باشد.");
    }

    private static bool IsOfferKind(string? value) =>
        value is not null && OfferKinds.Contains(value, StringComparer.OrdinalIgnoreCase);
}
