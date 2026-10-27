using FluentValidation;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog;

public sealed class EntertainmentProgramsValidator : AbstractValidator<GetEntertainmentProgramsQuery>
{
    public EntertainmentProgramsValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("شرح جستجو حداکثر ۱۰۰ کاراکتر است");
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 1000).WithMessage("شماره صفحه معتبر نیست");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه صفحه معتبر نیست");
        RuleFor(x => x.LocationCode).MaximumLength(200).WithMessage("موقعیت معتبر نیست");
        RuleFor(x => x.CategoryCode).MaximumLength(100).WithMessage("دسته‌بندی معتبر نیست");
        RuleFor(x => x.ProviderKey).Must(x => string.IsNullOrWhiteSpace(x) || x == "touristpanel").WithMessage("ارائه‌دهنده معتبر نیست");
    }
}
public sealed class EntertainmentProgramValidator : AbstractValidator<GetEntertainmentProgramQuery>
{
    public EntertainmentProgramValidator() => RuleFor(x => x.ProgramKey).Must(x => Guid.TryParse(x, out var id) && id != Guid.Empty).WithMessage("شناسه خدمت معتبر نیست");
}
