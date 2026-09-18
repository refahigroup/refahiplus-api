using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.TerminateOrderPayment;

public sealed class TerminateOrderPaymentCommandValidator : AbstractValidator<TerminateOrderPaymentCommand>
{
    public TerminateOrderPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ReleaseReference).NotEmpty().MaximumLength(128)
            .Matches("^[!-~]+$").WithMessage("مرجع خاتمه پرداخت نامعتبر است");
    }
}
