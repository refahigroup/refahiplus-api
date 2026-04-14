using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.AddToCart;

public class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");
    }
}
