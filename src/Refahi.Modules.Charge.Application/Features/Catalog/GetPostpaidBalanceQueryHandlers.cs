using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetPostpaidBalanceValidator : AbstractValidator<GetPostpaidBalanceQuery>
{
    public GetPostpaidBalanceValidator()
    {
        RuleFor(x => x.Operator).IsInEnum()
            .WithMessage("اپراتور معتبر نیست");

        RuleFor(x => x.MobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");
    }
}

public sealed class GetPostpaidBalanceQueryHandlers : IRequestHandler<GetPostpaidBalanceQuery, ChargePostpaidBalanceDto>
{
    private readonly IChargeProviderResolver _providers;

    public GetPostpaidBalanceQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public Task<ChargePostpaidBalanceDto> Handle(GetPostpaidBalanceQuery q, CancellationToken ct)
    {
        return _providers.GetDefault()
                         .GetPostpaidBalanceAsync(q.Operator, q.MobileNumber, ct);
    }

}
