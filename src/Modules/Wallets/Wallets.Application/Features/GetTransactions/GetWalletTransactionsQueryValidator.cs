using FluentValidation;
using Wallets.Application.Contracts.Features.GetTransactions;

namespace Wallets.Application.Features.GetTransactions;

public class GetWalletTransactionsQueryValidator : AbstractValidator<GetWalletTransactionsQuery>
{
    public GetWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}
