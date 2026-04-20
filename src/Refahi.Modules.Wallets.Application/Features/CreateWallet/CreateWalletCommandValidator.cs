using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Features.CreateWallet;

public class CreateWalletCommandValidator : AbstractValidator<CreateWalletCommand>
{
    private static readonly HashSet<string> AllowedWalletTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "REFAHI"
    };

    public CreateWalletCommandValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("شناسه مالک الزامی است");

        RuleFor(x => x.WalletType)
            .NotEmpty().WithMessage("نوع کیف‌پول الزامی است")
            .Must(t => AllowedWalletTypes.Contains(t))
            .WithMessage("نوع کیف‌پول باید REFAHI باشد");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("ارز الزامی است")
            .Must(c => string.Equals(c, "IRR", StringComparison.OrdinalIgnoreCase))
            .WithMessage("تنها ارز پشتیبانی‌شده IRR است");
    }
}
