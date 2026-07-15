using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductCatalogV2;

public sealed class GetProductCatalogV2QueryValidator : AbstractValidator<GetProductCatalogV2Query>
{
    public GetProductCatalogV2QueryValidator()
    {
        RuleFor(x => x.SearchQuery).MaximumLength(200).WithMessage("عبارت جست‌وجو حداکثر ۲۰۰ کاراکتر است.");
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue)
            .WithMessage("دسته‌بندی معتبر نیست.");
        RuleFor(x => x.ShopId).NotEqual(Guid.Empty).When(x => x.ShopId.HasValue)
            .WithMessage("شناسه فروشگاه معتبر نیست.");
        RuleFor(x => x.SalesModel).Must(IsSalesModel).When(x => !string.IsNullOrWhiteSpace(x.SalesModel))
            .WithMessage("مدل فروش معتبر نیست.");
        RuleFor(x => x.MinPriceMinor).GreaterThanOrEqualTo(0).When(x => x.MinPriceMinor.HasValue)
            .WithMessage("حداقل قیمت نمی‌تواند منفی باشد.");
        RuleFor(x => x.MaxPriceMinor).GreaterThanOrEqualTo(0).When(x => x.MaxPriceMinor.HasValue)
            .WithMessage("حداکثر قیمت نمی‌تواند منفی باشد.");
        RuleFor(x => x).Must(x => !x.MinPriceMinor.HasValue || !x.MaxPriceMinor.HasValue || x.MinPriceMinor <= x.MaxPriceMinor)
            .WithMessage("حداقل قیمت باید کوچک‌تر یا مساوی حداکثر قیمت باشد.");
        RuleFor(x => x.Sort).Must(IsSort).WithMessage("مرتب‌سازی معتبر نیست.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("شماره صفحه باید حداقل ۱ باشد.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 30).WithMessage("اندازه صفحه باید بین ۱ تا ۳۰ باشد.");
    }

    internal static bool IsSalesModel(string? value) =>
        value is not null && (value.Equals("StockBased", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("SessionBased", StringComparison.OrdinalIgnoreCase));

    internal static bool IsSort(string value) =>
        value is not null && (value.Equals("newest", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("price-asc", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("price-desc", StringComparison.OrdinalIgnoreCase));
}

