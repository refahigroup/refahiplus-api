using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;

namespace Refahi.Modules.Store.Application.Features.Shops.CreateShop;

public class CreateShopCommandValidator : AbstractValidator<CreateShopCommand>
{
    public CreateShopCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام فروشگاه الزامی است")
            .MaximumLength(200).WithMessage("نام فروشگاه نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("اسلاگ الزامی است")
            .MaximumLength(200).WithMessage("اسلاگ نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")
            .Matches(@"^[a-z0-9-]+$").WithMessage("اسلاگ فقط می‌تواند شامل حروف کوچک انگلیسی، اعداد و خط تیره باشد");

        RuleFor(x => x.ShopType)
            .Must(t => t is 1 or 2 or 3).WithMessage("نوع فروشگاه نامعتبر است");

        RuleFor(x => x.ProviderId)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");
    }
}
