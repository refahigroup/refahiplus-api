using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.ExecuteOrderPayment;

public sealed class ExecuteOrderPaymentCommandValidator
    : AbstractValidator<ExecuteOrderPaymentCommand>
{
    public ExecuteOrderPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.DestinationWalletId).NotEmpty().WithMessage("مقصد مالی معتبر نیست");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200)
            .WithMessage("کلید تکرارپذیری معتبر نیست");
    }
}
