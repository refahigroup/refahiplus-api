using FluentValidation;

namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed class ResolveCommerceOperationValidator : AbstractValidator<ResolveCommerceOperationCommand>
{
    private static readonly string[] Outcomes = ["fulfilled", "notcreated", "cancelled"];
    public ResolveCommerceOperationValidator()
    {
        RuleFor(x => x.FulfillmentId)
            .NotEmpty()
            .WithMessage("شناسه fulfillment الزامی است");

        RuleFor(x => x.Outcome)
            .Must(x => Outcomes.Contains(x.Trim().ToLowerInvariant()))
            .WithMessage("نتیجه بررسی معتبر نیست");

        RuleFor(x => x.Evidence)
            .NotEmpty()
            .MaximumLength(4000)
            .WithMessage("مستند بررسی الزامی است");
    }
}
