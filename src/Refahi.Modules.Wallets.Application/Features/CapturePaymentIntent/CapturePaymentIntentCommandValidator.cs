using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;

namespace Refahi.Modules.Wallets.Application.Features.CapturePaymentIntent;

public sealed class CapturePaymentIntentCommandValidator : AbstractValidator<CapturePaymentIntentCommand>
{
    public CapturePaymentIntentCommandValidator()
    {
        RuleFor(x => x.IntentId).NotEmpty().WithMessage("IntentId is required");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required");
    }
}
