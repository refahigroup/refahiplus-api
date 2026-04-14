using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;

namespace Refahi.Modules.Store.Application.Features.Shops.UpdateShop;

public class UpdateShopCommandValidator : AbstractValidator<UpdateShopCommand>
{
    public UpdateShopCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("شناسه فروشگاه الزامی است");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام فروشگاه الزامی است")
            .MaximumLength(200).WithMessage("نام فروشگاه نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");
    }
}
