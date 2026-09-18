using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.CreateOrGetExternalPaymentOrder;

public sealed class CreateOrGetExternalPaymentOrderCommandValidator
    : AbstractValidator<CreateOrGetExternalPaymentOrderCommand>
{
    public CreateOrGetExternalPaymentOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.SourceModule)
            .NotEmpty().WithMessage("ماژول مبدا الزامی است")
            .MaximumLength(50).WithMessage("ماژول مبدا نمی‌تواند بیشتر از ۵۰ کاراکتر باشد");
        RuleFor(x => x.SourceReferenceId).NotEmpty().WithMessage("شناسه مرجع مبدا الزامی است");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("مبلغ سفارش باید مثبت باشد");
        RuleFor(x => x.Currency).Equal("IRR").WithMessage("ارز سفارش باید IRR باشد");
        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("کد دسته‌بندی الزامی است")
            .MaximumLength(100).WithMessage("کد دسته‌بندی نامعتبر است");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان سفارش الزامی است")
            .MaximumLength(300).WithMessage("عنوان سفارش بیش از حد مجاز است");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی بیش از حد مجاز است");
        RuleFor(x => x.PayableUntil)
            .Must(value => value > DateTimeOffset.UtcNow)
            .WithMessage("مهلت پرداخت سفارش باید در آینده باشد");
    }
}
