using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrderV3;

public sealed class PlaceStoreOrderV3CommandValidator : AbstractValidator<PlaceStoreOrderV3Command>
{
    public PlaceStoreOrderV3CommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.ModuleId).GreaterThan(0).WithMessage("ماژول فروشگاه نامعتبر است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128)
            .WithMessage("کلید یکتایی الزامی است و حداکثر ۱۲۸ کاراکتر دارد");
    }
}
