using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Vouchers;

namespace Refahi.Modules.Store.Application.Features.Vouchers;

public sealed class RedeemVoucherValidator : AbstractValidator<RedeemVoucherCommand>
{
    public RedeemVoucherValidator()
    {
        RuleFor(x => x.VendorUserId).NotEmpty().WithMessage("شناسه کاربر فروشنده الزامی است");
        RuleFor(x => x.ShopId).NotEmpty().WithMessage("شناسه فروشگاه الزامی است");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(128).WithMessage("کد ووچر معتبر نیست");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128)
            .WithMessage("کلید یکتایی الزامی است و حداکثر ۱۲۸ نویسه دارد");
    }
}

public sealed class GetVendorVoucherRedemptionHistoryValidator
    : AbstractValidator<GetVendorVoucherRedemptionHistoryQuery>
{
    public GetVendorVoucherRedemptionHistoryValidator()
    {
        RuleFor(x => x.VendorUserId).NotEmpty().WithMessage("شناسه کاربر فروشنده الزامی است");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000).WithMessage("شماره صفحه معتبر نیست");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد");
    }
}

public sealed class PrepareStoreOrderRefundValidator : AbstractValidator<PrepareStoreOrderRefundCommand>
{
    public PrepareStoreOrderRefundValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("دلیل بازگشت وجه الزامی است");
    }
}

public sealed class GetAdminStoreOrderRefundValidator : AbstractValidator<GetAdminStoreOrderRefundQuery>
{
    public GetAdminStoreOrderRefundValidator() => RuleFor(x => x.OrderId).NotEmpty()
        .WithMessage("شناسه سفارش الزامی است");
}

public sealed class OverrideRedeemedVoucherRefundValidator
    : AbstractValidator<OverrideRedeemedVoucherRefundCommand>
{
    public OverrideRedeemedVoucherRefundValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage("شناسه مدیر الزامی است");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("دلیل استثنای بازگشت وجه الزامی است و حداکثر ۵۰۰ نویسه دارد");
        RuleFor(x => x.Reason).Must(x => x is not null && x.Trim().Length >= 10)
            .WithMessage("دلیل استثنا باید حداقل ۱۰ نویسه معنادار داشته باشد");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128)
            .WithMessage("کلید یکتایی الزامی است و حداکثر ۱۲۸ نویسه دارد");
    }
}
