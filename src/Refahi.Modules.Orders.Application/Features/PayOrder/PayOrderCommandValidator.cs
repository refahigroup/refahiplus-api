using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.PayOrder;

public class PayOrderCommandValidator : AbstractValidator<PayOrderCommand>
{
    public PayOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("شناسه سفارش الزامی است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Allocations)
            .NotEmpty().WithMessage("حداقل یک کیف‌پول برای پرداخت الزامی است");

        RuleForEach(x => x.Allocations).ChildRules(alloc =>
        {
            alloc.RuleFor(a => a.WalletId)
                .NotEmpty().WithMessage("شناسه کیف‌پول الزامی است");

            alloc.RuleFor(a => a.AmountMinor)
                .GreaterThan(0).WithMessage("مبلغ پرداختی باید بزرگ‌تر از صفر باشد");
        });
    }
}
