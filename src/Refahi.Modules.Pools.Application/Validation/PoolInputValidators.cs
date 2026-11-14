using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Validation;
public sealed class PoolChoiceValidator : AbstractValidator<PoolChoice>
{
    public PoolChoiceValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("سانس معتبر نیست");
        RuleFor(x => x.AdultCount).InclusiveBetween(0, 100).WithMessage("تعداد بزرگسال معتبر نیست");
        RuleFor(x => x.ChildCount).InclusiveBetween(0, 100).WithMessage("تعداد کودک معتبر نیست");
        RuleFor(x => x).Must(x => (long)x.AdultCount + x.ChildCount is >= 1 and <= 100).WithMessage("تعداد بلیط باید بین یک تا صد باشد");
    }
}
public sealed class PoolCatalogFilterValidator : AbstractValidator<PoolCatalogFilter>
{
    public PoolCatalogFilterValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000).WithMessage("شماره صفحه معتبر نیست");
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("اندازه صفحه معتبر نیست");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("عبارت جست‌وجو طولانی است");
        RuleFor(x => x.State).MaximumLength(128).WithMessage("استان معتبر نیست");
        RuleFor(x => x.City).MaximumLength(128).WithMessage("شهر معتبر نیست");
        RuleFor(x => x.Gender).Must(x => x is null or 0 or 1).WithMessage("جنسیت معتبر نیست");
        RuleFor(x => x.Sort).Must(x => x is "name" or "date" or "price" or "rating").WithMessage("مرتب‌سازی معتبر نیست");
    }
}
