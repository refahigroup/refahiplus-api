using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;

namespace Refahi.Modules.Store.Application.Features.Products.GetProducts;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    private static readonly string[] AllowedSorts = ["newest", "price-asc", "price-desc"];

    public GetProductsQueryValidator()
    {
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه معتبر نیست");
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("شماره صفحه باید بیشتر از صفر باشد");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 30).WithMessage("تعداد نتایج هر صفحه باید بین ۱ تا ۳۰ باشد");
        RuleFor(x => x.Sort).Must(x => AllowedSorts.Contains(x)).WithMessage("مرتب‌سازی انتخاب‌شده معتبر نیست");
        RuleFor(x => x.SearchQuery).MaximumLength(200).WithMessage("عبارت جستجو نمی‌تواند بیشتر از ۲۰۰ نویسه باشد");
    }
}
