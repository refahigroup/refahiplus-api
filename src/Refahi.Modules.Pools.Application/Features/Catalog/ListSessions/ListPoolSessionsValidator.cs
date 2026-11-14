using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Catalog.ListSessions;
public sealed class ListPoolSessionsValidator : AbstractValidator<ListPoolSessionsQuery>
{
    public ListPoolSessionsValidator() { RuleFor(x => x.Filter).NotNull().WithMessage("ورودی الزامی است").SetValidator(new PoolCatalogFilterValidator()); }
}
