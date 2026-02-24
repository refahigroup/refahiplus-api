using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;

namespace Refahi.Modules.Wallets.Application.Features.CreatePaymentIntent;

public sealed class CreatePaymentIntentCommandValidator : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required");
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("Amount must be positive");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("Currency must be 3-letter code");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required");
        RuleFor(x => x.Allocations).NotNull().NotEmpty().WithMessage("Allocations cannot be empty");
    }
}
