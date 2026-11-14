using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Catalog.GetVenue;
public sealed class GetPoolVenueValidator : AbstractValidator<GetPoolVenueQuery>
{
    public GetPoolVenueValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه استخر معتبر نیست"); }
}
