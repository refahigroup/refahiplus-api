using FluentValidation;
using Refahi.Modules.References.Application.Contracts.Queries;
namespace Refahi.Modules.References.Application.Features.Cities.GetCities;
public sealed class GetCitiesQueryValidator : AbstractValidator<GetCitiesQuery>
{
    public GetCitiesQueryValidator() { RuleFor(x => x.Q).MaximumLength(100).WithMessage("عبارت جستجو نباید بیشتر از ۱۰۰ نویسه باشد."); }
}
