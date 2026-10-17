using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Validation;
namespace Refahi.Modules.Pools.Application.Features.Vouchers.Validate;
public sealed class ValidatePoolVoucherValidator : AbstractValidator<ValidatePoolVoucherQuery>
{
    public ValidatePoolVoucherValidator() { RuleFor(x => x.OperatorId).NotEmpty().WithMessage("کاربر معتبر نیست"); RuleFor(x => x.PoolId).NotEmpty().WithMessage("استخر معتبر نیست"); RuleFor(x => x.Code).NotEmpty().MaximumLength(128).WithMessage("کد ووچر معتبر نیست"); }
}
