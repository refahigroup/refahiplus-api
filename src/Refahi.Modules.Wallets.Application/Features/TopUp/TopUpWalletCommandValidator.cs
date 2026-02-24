using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

namespace Refahi.Modules.Wallets.Application.Features.TopUp;

public sealed class TopUpWalletCommandValidator : AbstractValidator<TopUpWalletCommand>
{
    public TopUpWalletCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.AmountMinor).GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(IsIso4217Alpha3)
            .WithMessage("Currency must be ISO-4217 alpha-3 (A-Z, length 3).");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MetadataJson).MaximumLength(4000);
        RuleFor(x => x.ExternalReference).MaximumLength(200);
    }

    private static bool IsIso4217Alpha3(string currency)
    {
        try
        {
            _ = Domain.Aggregates.Wallet.NormalizeCurrency(currency);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
