using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;

public sealed class PlaceStoreOrderCommandValidator : AbstractValidator<PlaceStoreOrderCommand>
{
    public PlaceStoreOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("کلید یکتایی الزامی است")
            .MaximumLength(200).WithMessage("کلید یکتایی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");
    }
}
