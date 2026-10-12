using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Application;

public sealed class PoolSelectionValidator : AbstractValidator<PoolSelection>
{
    public PoolSelectionValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست").Matches("^[a-z0-9-]+$").WithMessage("تأمین‌کننده معتبر نیست");
        RuleFor(x => x.PoolId).NotEmpty().WithMessage("شناسه استخر معتبر نیست").MaximumLength(128).WithMessage("شناسه استخر معتبر نیست");
        RuleFor(x => x.SessionRowId).NotEmpty().WithMessage("شناسه سانس معتبر نیست").MaximumLength(128).WithMessage("شناسه سانس معتبر نیست");
        RuleFor(x => x.AdultCount).InclusiveBetween(0, 100).WithMessage("تعداد بزرگسال معتبر نیست");
        RuleFor(x => x.ChildCount).InclusiveBetween(0, 100).WithMessage("تعداد کودک معتبر نیست");
        RuleFor(x => x).Must(x => (long)x.AdultCount + x.ChildCount is >= 1 and <= 100).WithMessage("مجموع تعداد بلیط باید بین یک و صد باشد");
    }
}
public sealed class PoolSearchValidator : AbstractValidator<PoolSearchQuery>
{
    public PoolSearchValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست");
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("شماره صفحه معتبر نیست").LessThanOrEqualTo(100000).WithMessage("شماره صفحه معتبر نیست");
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("اندازه صفحه معتبر نیست");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("عبارت جست‌وجو طولانی است");
        RuleFor(x => x.Sort).MaximumLength(64).WithMessage("مرتب‌سازی معتبر نیست");
        RuleFor(x => x.StateId).MaximumLength(128).WithMessage("شناسه استان معتبر نیست");
        RuleFor(x => x.CityId).MaximumLength(128).WithMessage("شناسه شهر معتبر نیست");
        RuleFor(x => x.AreaId).MaximumLength(128).WithMessage("شناسه منطقه معتبر نیست");
        RuleFor(x => x.Gender).Must(x => x is null or 0 or 1).WithMessage("جنسیت معتبر نیست");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).WithMessage("عرض جغرافیایی معتبر نیست");
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).WithMessage("طول جغرافیایی معتبر نیست");
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue).WithMessage("هر دو مختصات را وارد کنید");
        RuleFor(x => x).Must(x => !x.Latitude.HasValue || string.IsNullOrWhiteSpace(x.CityId) && string.IsNullOrWhiteSpace(x.AreaId))
            .WithMessage("شهر و منطقه با مختصات همزمان مجاز نیستند");
    }
}
public sealed class PoolQuoteValidator : AbstractValidator<PoolQuoteQuery>
{
    public PoolQuoteValidator() { RuleFor(x => x.Selection).NotNull().WithMessage("انتخاب سانس الزامی است").SetValidator(new PoolSelectionValidator()); }
}
public sealed class ReservePoolBookingValidator : AbstractValidator<ReservePoolBookingCommand>
{
    public ReservePoolBookingValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");
        RuleFor(x => x.Selection).NotNull().WithMessage("انتخاب سانس الزامی است").SetValidator(new PoolSelectionValidator());
        RuleFor(x => x.ExpectedTotalMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("کلید درخواست معتبر نیست").MaximumLength(128).WithMessage("کلید درخواست معتبر نیست");
    }
}
public sealed class CheckoutPoolBookingValidator : AbstractValidator<CheckoutPoolBookingCommand>
{
    public CheckoutPoolBookingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه رزرو معتبر نیست"); RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");
        RuleFor(x => x.Version).GreaterThan(0).WithMessage("نسخه رزرو معتبر نیست"); RuleFor(x => x.TotalMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست");
    }
}
public sealed class PoolDetailsValidator : AbstractValidator<PoolDetailsQuery>
{
    public PoolDetailsValidator() { RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده الزامی است"); RuleFor(x => x.PoolId).NotEmpty().WithMessage("شناسه استخر معتبر نیست").MaximumLength(128).WithMessage("شناسه استخر معتبر نیست"); }
}
public sealed class PoolStatesValidator : AbstractValidator<PoolStatesQuery>
{
    public PoolStatesValidator() { RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست"); }
}
public sealed class PoolCitiesValidator : AbstractValidator<PoolCitiesQuery>
{
    public PoolCitiesValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست");
        RuleFor(x => x.StateId).MaximumLength(128).WithMessage("شناسه استان معتبر نیست");
    }
}
public sealed class PoolSessionsValidator : AbstractValidator<PoolSessionsQuery>
{
    public PoolSessionsValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست");
        RuleFor(x => x.PoolId).NotEmpty().WithMessage("شناسه استخر معتبر نیست").MaximumLength(128).WithMessage("شناسه استخر معتبر نیست");
    }
}
public sealed class PoolTermsValidator : AbstractValidator<PoolTermsQuery>
{
    public PoolTermsValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("تأمین‌کننده معتبر نیست").MaximumLength(32).WithMessage("تأمین‌کننده معتبر نیست");
        RuleFor(x => x.PoolId).NotEmpty().WithMessage("شناسه استخر معتبر نیست").MaximumLength(128).WithMessage("شناسه استخر معتبر نیست");
    }
}
public sealed class GetPoolBookingValidator : AbstractValidator<GetPoolBookingQuery>
{
    public GetPoolBookingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه رزرو معتبر نیست");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");
    }
}
public sealed class CancelPoolBookingValidator : AbstractValidator<CancelPoolBookingCommand>
{
    public CancelPoolBookingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه رزرو معتبر نیست");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");
    }
}

