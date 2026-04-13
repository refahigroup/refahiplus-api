using FluentValidation;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Enums;

namespace Refahi.Modules.Orders.Application.Features.UpdateOrderStatus;

public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    private static readonly short[] ValidStatuses = [(short)OrderStatus.Processing, (short)OrderStatus.Shipped, (short)OrderStatus.Delivered, (short)OrderStatus.Cancelled];

    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("شناسه سفارش الزامی است");

        RuleFor(x => x.NewStatus)
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("وضعیت سفارش معتبر نیست");
    }
}
