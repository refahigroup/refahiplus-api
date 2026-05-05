using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Addresses.UpdateAddress;

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر نامعتبر است");
        RuleFor(x => x.Title).NotEmpty().WithMessage("عنوان آدرس الزامی است").MaximumLength(100);
        RuleFor(x => x.ProvinceId).GreaterThan(0).WithMessage("استان نامعتبر است");
        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("شهر نامعتبر است");
        RuleFor(x => x.FullAddress).NotEmpty().WithMessage("متن آدرس الزامی است").MaximumLength(1000);
        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("کد پستی الزامی است")
            .Length(10).WithMessage("کد پستی باید ۱۰ رقم باشد")
            .Matches(@"^\d{10}$").WithMessage("کد پستی فقط شامل عدد است");
        RuleFor(x => x.ReceiverName).NotEmpty().WithMessage("نام تحویل‌گیرنده الزامی است").MaximumLength(200);
        RuleFor(x => x.ReceiverPhone)
            .NotEmpty().WithMessage("شماره تحویل‌گیرنده الزامی است")
            .Matches(@"^09\d{9}$").WithMessage("شماره تحویل‌گیرنده نامعتبر است");
        RuleFor(x => x.Plate).MaximumLength(20);
        RuleFor(x => x.Unit).MaximumLength(20);
    }
}
