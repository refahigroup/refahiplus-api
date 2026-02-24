using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Commands;

namespace Refahi.Modules.Wallets.Application.Validators;

public sealed class RebuildBalancesBatchCommandValidator : AbstractValidator<RebuildBalancesBatchCommand>
{
    public RebuildBalancesBatchCommandValidator()
    {
        When(x => x.Currency is not null, () =>
        {
            RuleFor(x => x.Currency)
                .Length(3).WithMessage("Currency must be 3 characters (ISO 4217)");
        });
    }
}
