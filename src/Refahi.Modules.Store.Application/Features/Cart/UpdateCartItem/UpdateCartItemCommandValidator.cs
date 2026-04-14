using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.UpdateCartItem;

public class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.CartItemId)
            .NotEmpty().WithMessage("شناسه آیتم سبد خرید الزامی است");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");
    }
}
