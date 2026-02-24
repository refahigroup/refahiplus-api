using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Wallets.Application.Features.ReleasePaymentIntent;

public sealed class ReleasePaymentIntentCommandValidator : AbstractValidator<ReleasePaymentIntentCommand>
{
    public ReleasePaymentIntentCommandValidator()
    {
        RuleFor(x => x.IntentId).NotEmpty().WithMessage("IntentId is required");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required");
    }
}
