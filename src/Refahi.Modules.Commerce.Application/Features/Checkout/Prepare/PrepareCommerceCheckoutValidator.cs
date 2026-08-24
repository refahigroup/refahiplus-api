using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed class PrepareCommerceCheckoutValidator : AbstractValidator<PrepareCommerceCheckoutCommand>
{
    public PrepareCommerceCheckoutValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().Length(3, 255)
            .WithMessage("نام دریافت‌کننده معتبر نیست");

        RuleFor(x => x.RecipientMobile)
            .NotEmpty()
            .Matches(@"^[0-9+]{10,14}$")
            .WithMessage("شماره موبایل معتبر نیست");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("کلید یکتایی الزامی است");
    }
}
