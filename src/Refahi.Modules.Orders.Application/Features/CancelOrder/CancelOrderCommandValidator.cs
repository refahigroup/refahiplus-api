using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.CancelOrder;

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("شناسه سفارش الزامی است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");
    }
}
