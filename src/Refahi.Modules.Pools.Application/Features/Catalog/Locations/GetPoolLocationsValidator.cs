using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.Locations;
public sealed class GetPoolLocationsValidator:AbstractValidator<GetPoolLocationsQuery>
{
    public GetPoolLocationsValidator(){RuleFor(x=>x.State).MaximumLength(128).WithMessage("استان معتبر نیست");}
}
