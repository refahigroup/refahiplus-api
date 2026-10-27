using FluentValidation;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.UpdateItem;

public sealed class UpdateEntertainmentCartItemValidator : AbstractValidator<UpdateEntertainmentCartItemCommand>
{
    public UpdateEntertainmentCartItemValidator() =>
        RuleFor(x => x.Quantity)
        .InclusiveBetween(1, 100)
        .WithMessage("تعداد باید بین ۱ تا ۱۰۰ باشد");
}
