using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.Revoke;
public sealed class RevokePoolOperatorValidator : AbstractValidator<RevokePoolOperatorCommand>
{
    public RevokePoolOperatorValidator()
    {
        RuleFor(x=>x.ActorId).NotEmpty().WithMessage("شناسه معتبر نیست");
        RuleFor(x=>x.GrantId).NotEmpty().WithMessage("شناسه معتبر نیست");
    }
}
