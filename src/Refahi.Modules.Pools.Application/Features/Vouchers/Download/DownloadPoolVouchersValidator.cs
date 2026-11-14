using FluentValidation;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Vouchers.Download;
public sealed class DownloadPoolVouchersValidator : AbstractValidator<DownloadPoolVouchersQuery>
{
    public DownloadPoolVouchersValidator()
    {
        RuleFor(x=>x.Id).NotEmpty().WithMessage("شناسه معتبر نیست");
        RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه معتبر نیست");
    }
}
