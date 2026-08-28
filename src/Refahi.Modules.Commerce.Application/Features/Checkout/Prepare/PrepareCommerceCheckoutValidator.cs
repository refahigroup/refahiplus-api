using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed class PrepareCommerceCheckoutValidator : AbstractValidator<PrepareCommerceCheckoutCommand>
{
    public PrepareCommerceCheckoutValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("کلید یکتایی الزامی است");
    }
}
