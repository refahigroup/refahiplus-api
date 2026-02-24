using FluentValidation;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;

namespace Refahi.Modules.Wallets.Application.Features.GetTransactions;

public class GetWalletTransactionsQueryValidator : AbstractValidator<GetWalletTransactionsQuery>
{
    public GetWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}
