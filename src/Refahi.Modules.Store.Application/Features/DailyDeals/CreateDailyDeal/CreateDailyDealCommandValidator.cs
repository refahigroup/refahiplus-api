using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.CreateDailyDeal;

public class CreateDailyDealCommandValidator : AbstractValidator<CreateDailyDealCommand>
{
    public CreateDailyDealCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(1, 99).WithMessage("درصد تخفیف باید بین ۱ تا ۹۹ باشد");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("زمان شروع الزامی است")
            .Must(t => DateTimeOffset.TryParse(t, out _))
            .WithMessage("زمان شروع وارد شده معتبر نیست");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("زمان پایان الزامی است")
            .Must(t => DateTimeOffset.TryParse(t, out _))
            .WithMessage("زمان پایان وارد شده معتبر نیست");
    }
}
