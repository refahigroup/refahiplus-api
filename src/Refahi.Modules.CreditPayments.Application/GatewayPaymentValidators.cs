using FluentValidation;
using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class CreateGatewayPaymentValidator : AbstractValidator<CreateGatewayPaymentCommand>
{
    public CreateGatewayPaymentValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty().WithMessage("پذیرنده الزامی است");
        RuleFor(x => x.TerminalId).NotEmpty().WithMessage("پایانه الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("کلید تکرارپذیری الزامی است");
        RuleFor(x => x.MerchantOrderId).NotEmpty().MaximumLength(100).WithMessage("شناسه سفارش پذیرنده نامعتبر است");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("مبلغ باید مثبت باشد");
        RuleFor(x => x.Currency).NotEmpty().WithMessage("ارز الزامی است");
        RuleFor(x => x.Description).MaximumLength(250).WithMessage("توضیحات حداکثر ۲۵۰ نویسه است");
        RuleFor(x => x.ReturnUrl).NotEmpty().MaximumLength(2048).WithMessage("نشانی بازگشت نامعتبر است");
    }
}

public sealed class SendCheckoutOtpValidator : AbstractValidator<SendCheckoutOtpCommand>
{
    public SendCheckoutOtpValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(100)
            .WithMessage("شناسه کاربر نامعتبر است");
    }
}

public sealed class VerifyCheckoutOtpValidator : AbstractValidator<VerifyCheckoutOtpCommand>
{
    public VerifyCheckoutOtpValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty().WithMessage("شناسه چالش الزامی است");
        RuleFor(x => x.Otp).NotEmpty().MaximumLength(20).WithMessage("کد تأیید نامعتبر است");
    }
}
