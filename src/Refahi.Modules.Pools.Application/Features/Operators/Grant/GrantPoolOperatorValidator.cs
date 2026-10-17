using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.Grant;
public sealed class GrantPoolOperatorValidator : AbstractValidator<GrantPoolOperatorCommand>
{
    public GrantPoolOperatorValidator()
    {
        RuleFor(x=>x.ActorId).NotEmpty().WithMessage("شناسه معتبر نیست");
        RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه معتبر نیست");
        RuleFor(x=>x.PoolId).NotEmpty().WithMessage("شناسه معتبر نیست");
    }
}
