using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Cart.UpdateItem;

public sealed class UpdateCommerceCartItemValidator : AbstractValidator<UpdateCommerceCartItemCommand>
{
    public UpdateCommerceCartItemValidator() =>
        RuleFor(x => x.Quantity)
        .InclusiveBetween(1, 100)
        .WithMessage("تعداد باید بین ۱ تا ۱۰۰ باشد");
}
