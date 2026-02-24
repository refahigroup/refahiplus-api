using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;

namespace Refahi.Modules.Wallets.Application.Features.RefundPayment;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency key is required.")
            .MaximumLength(256)
            .WithMessage("Idempotency key must not exceed 256 characters.");

        RuleFor(x => x.Reason)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("Reason must not exceed 2000 characters.");
    }
}
