using FluentValidation;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Shared.Monetary;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator(IOptions<FundingSafetyOptions> safetyOptions)
    {
        var options = safetyOptions.Value;
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است.");

        RuleFor(x => x.WalletId).NotEmpty().WithMessage("شناسه کیف‌پول الزامی است.");

        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");

        RuleFor(x => x.Currency)
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage("تنها ارز پشتیبانی‌شده IRR است.");

        RuleFor(x => x.ReturnBaseUrl)
            .NotEmpty()
            .WithMessage("آدرس بازگشت الزامی است.")
            .MaximumLength(500)
            .WithMessage("آدرس بازگشت نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")
            .Must(url => FundingReturnUrlPolicy.IsAllowed(url, options))
            .WithMessage("آدرس بازگشت مجاز نیست.");

        RuleFor(x => x.SucceededCallbackUrl)
            .MaximumLength(500)
            .Must(url => FundingReturnUrlPolicy.IsAllowed(url, options))
            .WithMessage("آدرس بازگشت موفق مجاز نیست.");

        RuleFor(x => x.FailedCallbackUrl)
            .MaximumLength(500)
            .Must(url => FundingReturnUrlPolicy.IsAllowed(url, options))
            .WithMessage("آدرس بازگشت ناموفق مجاز نیست.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("هدر Idempotency-Key الزامی است.")
            .MaximumLength(150).WithMessage("کلید تکرار بیش از حد طولانی است.");
    }
}
