using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;

public class PlaceStoreOrderCommandValidator : AbstractValidator<PlaceStoreOrderCommand>
{
    public PlaceStoreOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.WalletAllocations)
            .NotEmpty().WithMessage("حداقل یک کیف‌پول برای پرداخت الزامی است");

        RuleForEach(x => x.WalletAllocations).ChildRules(alloc =>
        {
            alloc.RuleFor(a => a.WalletId)
                .NotEmpty().WithMessage("شناسه کیف‌پول الزامی است");

            alloc.RuleFor(a => a.AmountMinor)
                .GreaterThan(0).WithMessage("مبلغ پرداختی باید بزرگ‌تر از صفر باشد");
        });

        RuleFor(x => x.WalletAllocations)
            .Must(allocs => allocs == null || allocs.Sum(a => a.AmountMinor) > 0)
            .WithMessage("مجموع مبالغ پرداختی باید بزرگ‌تر از صفر باشد");
    }
}
