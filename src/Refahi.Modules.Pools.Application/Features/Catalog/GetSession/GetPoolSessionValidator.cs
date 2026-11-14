using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Catalog.GetSession;
public sealed class GetPoolSessionValidator : AbstractValidator<GetPoolSessionQuery>
{
    public GetPoolSessionValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه سانس معتبر نیست"); }
}
