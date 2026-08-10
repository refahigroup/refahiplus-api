using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Vendor;

namespace Refahi.Modules.Store.Application.Features.Vendor;

public sealed class GetVendorStoreOrderByOrderIdQueryValidator
    : AbstractValidator<GetVendorStoreOrderByOrderIdQuery>
{
    public GetVendorStoreOrderByOrderIdQueryValidator()
    {
        RuleFor(x => x.VendorUserId).NotEmpty().WithMessage("شناسه Vendor الزامی است");
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
    }
}

public sealed class GetVendorStoreOrdersByOrderIdsQueryValidator
    : AbstractValidator<GetVendorStoreOrdersByOrderIdsQuery>
{
    public GetVendorStoreOrdersByOrderIdsQueryValidator()
    {
        RuleFor(x => x.VendorUserId).NotEmpty().WithMessage("شناسه Vendor الزامی است");
        RuleFor(x => x.OrderIds).NotNull().NotEmpty().WithMessage("حداقل یک شناسه سفارش الزامی است")
            .Must(x => x is { Count: <= 100 }).WithMessage("حداکثر ۱۰۰ سفارش قابل استعلام است");
        RuleForEach(x => x.OrderIds).NotEmpty().WithMessage("شناسه سفارش نامعتبر است");
    }
}

public sealed class GetUserStoreOrderByOrderIdQueryValidator
    : AbstractValidator<GetUserStoreOrderByOrderIdQuery>
{
    public GetUserStoreOrderByOrderIdQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("شناسه سفارش الزامی است");
    }
}

public sealed class GetUserStoreOrdersByOrderIdsQueryValidator
    : AbstractValidator<GetUserStoreOrdersByOrderIdsQuery>
{
    public GetUserStoreOrdersByOrderIdsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.OrderIds).NotNull().NotEmpty().WithMessage("حداقل یک شناسه سفارش الزامی است")
            .Must(x => x is { Count: <= 100 }).WithMessage("حداکثر ۱۰۰ سفارش قابل استعلام است");
        RuleForEach(x => x.OrderIds).NotEmpty().WithMessage("شناسه سفارش نامعتبر است");
    }
}
