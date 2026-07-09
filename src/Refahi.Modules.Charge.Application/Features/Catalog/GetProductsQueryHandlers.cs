using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
    {
        RuleFor(x => x.Operator)
            .IsInEnum()
            .WithMessage("اپراتور معتبر نیست");
    }
}


public sealed class GetProductsQueryHandlers : IRequestHandler<GetProductsQuery, IReadOnlyList<ChargeProductDto>>
{
    private readonly IChargeProviderResolver _providers;

    public GetProductsQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public Task<IReadOnlyList<ChargeProductDto>> Handle(GetProductsQuery q, CancellationToken ct)
    {
        return _providers.GetDefault().GetProductsAsync(q.Operator, ct);
    }
}