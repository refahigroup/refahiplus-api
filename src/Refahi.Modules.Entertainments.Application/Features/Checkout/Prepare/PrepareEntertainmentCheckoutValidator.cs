using FluentValidation;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;

public sealed class PrepareEntertainmentCheckoutValidator : AbstractValidator<PrepareEntertainmentCheckoutCommand>
{
    public PrepareEntertainmentCheckoutValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("کلید یکتایی الزامی است");
    }
}
