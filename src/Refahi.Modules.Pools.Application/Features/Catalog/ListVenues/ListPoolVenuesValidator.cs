using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Catalog.ListVenues;
public sealed class ListPoolVenuesValidator : AbstractValidator<ListPoolVenuesQuery>
{
    public ListPoolVenuesValidator() { RuleFor(x => x.Filter).NotNull().WithMessage("ورودی الزامی است").SetValidator(new PoolCatalogFilterValidator()); }
}
