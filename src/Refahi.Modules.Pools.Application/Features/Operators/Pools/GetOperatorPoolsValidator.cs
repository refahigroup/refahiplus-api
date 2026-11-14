using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.Pools;
public sealed class GetOperatorPoolsValidator : AbstractValidator<GetOperatorPoolsQuery>
{
    public GetOperatorPoolsValidator()
    {
        RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه معتبر نیست");
    }
}
