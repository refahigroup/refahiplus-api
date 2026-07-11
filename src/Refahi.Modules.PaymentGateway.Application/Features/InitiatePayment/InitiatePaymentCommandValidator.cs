using FluentValidation;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Shared.Monetary;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است.");

        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("شناسه کیف‌پول الزامی است.");

        RuleFor(x => x.AmountMinor)
            .GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");

        RuleFor(x => x.Currency)
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage("تنها ارز پشتیبانی‌شده IRR است.");

        RuleFor(x => x.ReturnBaseUrl)
            .NotEmpty().WithMessage("آدرس بازگشت الزامی است.")
            .MaximumLength(500).WithMessage("آدرس بازگشت نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.");
    }
}
