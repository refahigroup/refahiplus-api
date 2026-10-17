using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Application.Validators;

public sealed class PoolSelectionValidator : AbstractValidator<PoolSelection>
{
    public PoolSelectionValidator()
    {
        RuleFor(x => x.ProviderKey)
            .NotEmpty()
            .WithMessage("تأمین‌کننده معتبر نیست")
            .MaximumLength(32)
            .WithMessage("تأمین‌کننده معتبر نیست")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("تأمین‌کننده معتبر نیست");

        RuleFor(x => x.PoolId)
            .NotEmpty()
            .WithMessage("شناسه استخر معتبر نیست")
            .MaximumLength(128)
            .WithMessage("شناسه استخر معتبر نیست");

        RuleFor(x => x.SessionRowId)
            .NotEmpty()
            .WithMessage("شناسه سانس معتبر نیست")
            .MaximumLength(128)
            .WithMessage("شناسه سانس معتبر نیست");

        RuleFor(x => x.AdultCount)
            .InclusiveBetween(0, 100)
            .WithMessage("تعداد بزرگسال معتبر نیست");

        RuleFor(x => x.ChildCount)
            .InclusiveBetween(0, 100)
            .WithMessage("تعداد کودک معتبر نیست");

        RuleFor(x => x)
            .Must(x => (long)x.AdultCount + x.ChildCount is >= 1 and <= 100)
            .WithMessage("مجموع تعداد بلیط باید بین یک و صد باشد");
    }
}
