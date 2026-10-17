using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Application.Validators;

public sealed class PoolSearchValidator : AbstractValidator<PoolSearchQuery>
{
    public PoolSearchValidator()
    {
        RuleFor(x => x.ProviderKey)
            .NotEmpty()
            .WithMessage("تأمین‌کننده معتبر نیست")
            .MaximumLength(32)
            .WithMessage("تأمین‌کننده معتبر نیست");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("شماره صفحه معتبر نیست")
            .LessThanOrEqualTo(100000)
            .WithMessage("شماره صفحه معتبر نیست");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("اندازه صفحه معتبر نیست");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("عبارت جست‌وجو طولانی است");

        RuleFor(x => x.Sort)
            .MaximumLength(64)
            .WithMessage("مرتب‌سازی معتبر نیست");

        RuleFor(x => x.StateId)
            .MaximumLength(128)
            .WithMessage("شناسه استان معتبر نیست");

        RuleFor(x => x.CityId)
            .MaximumLength(128)
            .WithMessage("شناسه شهر معتبر نیست");

        RuleFor(x => x.AreaId)
            .MaximumLength(128)
            .WithMessage("شناسه منطقه معتبر نیست");

        RuleFor(x => x.Gender)
            .Must(x => x is null or 0 or 1)
            .WithMessage("جنسیت معتبر نیست");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
            .WithMessage("عرض جغرافیایی معتبر نیست");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .WithMessage("طول جغرافیایی معتبر نیست");

        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("هر دو مختصات را وارد کنید");

        RuleFor(x => x)
            .Must(x => !x.Latitude.HasValue || string.IsNullOrWhiteSpace(x.CityId) && string.IsNullOrWhiteSpace(x.AreaId))
            .WithMessage("شهر و منطقه با مختصات همزمان مجاز نیستند");
    }
}

