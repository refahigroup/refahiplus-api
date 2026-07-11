using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Refahi.Shared.Monetary;

namespace Refahi.Modules.Wallets.Application.Features.TopUp;

public sealed class TopUpWalletCommandValidator : AbstractValidator<TopUpWalletCommand>
{
    public TopUpWalletCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.AmountMinor).GreaterThan(0);

        RuleFor(x => x.Currency)
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage("تنها ارز پشتیبانی‌شده IRR است");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MetadataJson).MaximumLength(4000);
        RuleFor(x => x.ExternalReference).MaximumLength(200);
    }

}
