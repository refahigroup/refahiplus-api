using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetPinCategoriesValidator : AbstractValidator<GetPinCategoriesQuery>
{
    public GetPinCategoriesValidator()
    {
        RuleFor(x => x.Operator!.Value)
            .IsInEnum()
            .When(x => x.Operator.HasValue)
            .WithMessage("اپراتور معتبر نیست");
    }
}

public sealed class GetPinCategoriesQueryHandlers : IRequestHandler<GetPinCategoriesQuery, IReadOnlyList<PinChargeCategoryDto>>
{
    private readonly IChargeProviderResolver _providers;

    public GetPinCategoriesQueryHandlers(IChargeProviderResolver providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<PinChargeCategoryDto>> Handle(GetPinCategoriesQuery q, CancellationToken ct)
    {
        var categories = await _providers.GetDefault().GetPinCategoriesAsync(ct);

        return q.Operator.HasValue
            ? categories.Where(x => x.Operator == q.Operator.Value).ToArray() 
            : categories;
    }
}
