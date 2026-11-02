using FluentValidation;
using Refahi.Modules.References.Application.Contracts.Queries;
namespace Refahi.Modules.References.Application.Features.Provinces.GetProvinces;
public sealed class GetProvincesQueryValidator : AbstractValidator<GetProvincesQuery>
{
    public GetProvincesQueryValidator() { RuleFor(x => x.Q).MaximumLength(100).WithMessage("عبارت جستجو نباید بیشتر از ۱۰۰ نویسه باشد."); }
}
