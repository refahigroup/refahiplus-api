using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;

public sealed class CancelCommerceOrderValidator : AbstractValidator<CancelCommerceOrderCommand>
{
    public CancelCommerceOrderValidator()
    {
        RuleFor(x => x.CommerceOrderId)
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
