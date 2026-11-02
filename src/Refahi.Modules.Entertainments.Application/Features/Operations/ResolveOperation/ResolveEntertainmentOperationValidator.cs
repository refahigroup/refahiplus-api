using FluentValidation;

namespace Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation;

public sealed class ResolveEntertainmentOperationValidator : AbstractValidator<ResolveEntertainmentOperationCommand>
{
    private static readonly string[] Outcomes = ["fulfilled", "notcreated"];
    public ResolveEntertainmentOperationValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("هویت مدیر معتبر نیست");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("کلید یکتایی معتبر نیست");
        RuleFor(x => x.FulfillmentId)
            .NotEmpty()
            .WithMessage("شناسه fulfillment الزامی است");

        RuleFor(x => x.Outcome)
            .Must(x => x != null && Outcomes.Contains(x.Trim().ToLowerInvariant()))
            .WithMessage("نتیجه بررسی معتبر نیست");

        RuleFor(x => x.Evidence)
            .NotEmpty()
            .MaximumLength(4000)
            .WithMessage("مستند بررسی الزامی است");
    }
}
