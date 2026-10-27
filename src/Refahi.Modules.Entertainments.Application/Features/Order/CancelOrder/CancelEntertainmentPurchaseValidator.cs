using FluentValidation;

namespace Refahi.Modules.Entertainments.Application.Features.Order.CancelOrder;

public sealed class CancelEntertainmentPurchaseValidator : AbstractValidator<CancelEntertainmentPurchaseCommand>
{
    public CancelEntertainmentPurchaseValidator()
    {
        RuleFor(x => x.EntertainmentPurchaseId)
            .NotEmpty()
            .WithMessage("شناسه سفارش الزامی است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("کلید یکتایی الزامی است");

        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .WithMessage("علت لغو بیش از حد مجاز است");
    }
}
