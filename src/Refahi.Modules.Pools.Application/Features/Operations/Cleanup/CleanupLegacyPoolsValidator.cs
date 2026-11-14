using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operations.Cleanup;
public sealed class CleanupLegacyPoolsValidator : AbstractValidator<CleanupLegacyPoolsCommand>
{
    public CleanupLegacyPoolsValidator()
    {
        RuleFor(x=>x.ActorId).NotEmpty().WithMessage("شناسه معتبر نیست");
    }
}
