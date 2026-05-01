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

        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("آدرس نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد");

        RuleFor(x => x.ManagerName)
            .MaximumLength(200).WithMessage("نام مدیر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.ManagerPhone)
            .MaximumLength(20).WithMessage("تلفن مدیر نمی‌تواند بیشتر از ۲۰ کاراکتر باشد")
            .Matches(@"^[0-9+\-()\s]*$").When(x => !string.IsNullOrEmpty(x.ManagerPhone))
            .WithMessage("فرمت تلفن مدیر نامعتبر است");

        RuleFor(x => x.RepresentativeName)
            .MaximumLength(200).WithMessage("نام نماینده نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.RepresentativePhone)
            .MaximumLength(20).WithMessage("تلفن نماینده نمی‌تواند بیشتر از ۲۰ کاراکتر باشد")
            .Matches(@"^[0-9+\-()\s]*$").When(x => !string.IsNullOrEmpty(x.RepresentativePhone))
            .WithMessage("فرمت تلفن نماینده نامعتبر است");

        RuleFor(x => x.ContactPhone)
            .MaximumLength(20).WithMessage("تلفن تماس نمی‌تواند بیشتر از ۲۰ کاراکتر باشد")
            .Matches(@"^[0-9+\-()\s]*$").When(x => !string.IsNullOrEmpty(x.ContactPhone))
            .WithMessage("فرمت تلفن تماس نامعتبر است");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
            .WithMessage("عرض جغرافیایی باید بین -۹۰ تا ۹۰ باشد");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
            .WithMessage("طول جغرافیایی باید بین -۱۸۰ تا ۱۸۰ باشد");
    }
}
