using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateOrgCreditWallet;
using Refahi.Shared.Monetary;

namespace Refahi.Modules.Wallets.Application.Features.CreateOrgCreditWallet;

public sealed class CreateOrgCreditWalletCommandValidator : AbstractValidator<CreateOrgCreditWalletCommand>
{
    public CreateOrgCreditWalletCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("شناسه مالک الزامی است");
        RuleFor(x => x.Currency)
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage("تنها ارز پشتیبانی‌شده IRR است");
    }
}
