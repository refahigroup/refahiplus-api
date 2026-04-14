using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;

namespace Refahi.Modules.Store.Application.Features.Sessions.CreateSession;

public class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("تاریخ الزامی است")
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage("تاریخ وارد شده معتبر نیست (مثال: 2025-02-15)");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("زمان شروع الزامی است")
            .Must(t => TimeOnly.TryParse(t, out _))
            .WithMessage("زمان شروع وارد شده معتبر نیست (مثال: 08:00)");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("زمان پایان الزامی است")
            .Must(t => TimeOnly.TryParse(t, out _))
            .WithMessage("زمان پایان وارد شده معتبر نیست (مثال: 10:00)");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("ظرفیت سانس باید بیشتر از صفر باشد");

        RuleFor(x => x.PriceAdjustment)
            .GreaterThanOrEqualTo(0).WithMessage("تفاوت قیمت نمی‌تواند منفی باشد");
    }
}
