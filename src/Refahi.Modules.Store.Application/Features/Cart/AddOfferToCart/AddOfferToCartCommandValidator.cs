using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.AddOfferToCart;

public sealed class AddOfferToCartCommandValidator : AbstractValidator<AddOfferToCartCommand>
{
    public AddOfferToCartCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
        RuleFor(x => x.OfferId).NotEmpty().WithMessage("شناسه پیشنهاد الزامی است");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد");
    }
}
