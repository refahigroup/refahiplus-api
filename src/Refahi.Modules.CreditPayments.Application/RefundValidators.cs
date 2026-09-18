using FluentValidation;
using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class CreateRefundCommandValidator:AbstractValidator<CreateRefundCommand>
{
    public CreateRefundCommandValidator()
    {
        RuleFor(x=>x.PaymentId).NotEmpty().WithMessage("شناسه پرداخت الزامی است");
        RuleFor(x=>x.IdempotencyKey).NotEmpty().Length(8,128).WithMessage("کلید تکرارپذیری نامعتبر است");
        RuleFor(x=>x.AmountMinor).GreaterThan(0).WithMessage("مبلغ بازگشت وجه باید مثبت باشد");
        RuleFor(x=>x.MerchantRefundReference).MaximumLength(100).WithMessage("مرجع بازگشت وجه حداکثر ۱۰۰ نویسه است");
        RuleFor(x=>x.Reason).MaximumLength(250).WithMessage("دلیل بازگشت وجه حداکثر ۲۵۰ نویسه است");
    }
}
