using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Features.UpdateOrderStatus;

public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("شناسه سفارش الزامی است");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("وضعیت سفارش معتبر نیست");
    }
}
