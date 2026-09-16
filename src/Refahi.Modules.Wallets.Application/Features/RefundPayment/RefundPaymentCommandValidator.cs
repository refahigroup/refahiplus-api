using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

namespace Refahi.Modules.Wallets.Application.Features.RefundPayment;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("شناسه پرداخت الزامی است.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("کلید تکرارپذیری الزامی است.")
            .MaximumLength(256)
            .WithMessage("کلید تکرارپذیری نباید بیش از ۲۵۶ نویسه باشد.");

        RuleFor(x => x.Reason)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("دلیل بازپرداخت نباید بیش از ۲۰۰۰ نویسه باشد.");

        RuleFor(x => x.AmountMinor)
            .GreaterThan(0)
            .When(x => x.AmountMinor.HasValue)
            .WithMessage("مبلغ بازپرداخت باید بیشتر از صفر باشد.");
    }
}
