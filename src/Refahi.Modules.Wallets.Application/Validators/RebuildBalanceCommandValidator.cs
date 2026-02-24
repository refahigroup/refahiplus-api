using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Commands;

namespace Refahi.Modules.Wallets.Application.Validators;

public sealed class RebuildBalanceCommandValidator : AbstractValidator<RebuildBalanceCommand>
{
    public RebuildBalanceCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("WalletId is required");
    }
}
