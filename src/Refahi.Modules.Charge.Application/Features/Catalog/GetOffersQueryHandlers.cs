using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetOffersValidator : AbstractValidator<GetOffersQuery>
{
    public GetOffersValidator()
    {
        RuleFor(x => x.Operator)
            .IsInEnum()
            .WithMessage("اپراتور معتبر نیست");

        RuleFor(x => x.MobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("دسته پیشنهاد معتبر نیست");
    }
}

public sealed class GetOffersQueryHandlers : IRequestHandler<GetOffersQuery, IReadOnlyList<ChargeProductDto>>
{
    private readonly IChargeProviderResolver _providers;

    public GetOffersQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public Task<IReadOnlyList<ChargeProductDto>> Handle(GetOffersQuery q, CancellationToken ct)
    {
        return _providers.GetDefault()
            .GetOffersAsync(q.Operator, q.MobileNumber, q.Category, ct);
    }

}
