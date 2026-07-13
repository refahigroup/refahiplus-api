using FluentValidation;

namespace Refahi.Modules.Identity.Application.Features.Addresses;

internal sealed class AddressInputValidator<T> : AbstractValidator<T> where T : IAddressInput
{
    public AddressInputValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(100).WithMessage("عنوان آدرس نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد");
        RuleFor(x => x.ProvinceId).GreaterThan(0).WithMessage("استان نامعتبر است");
        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("شهر نامعتبر است");
        RuleFor(x => x.FullAddress)
            .NotEmpty().WithMessage("متن آدرس الزامی است")
            .MaximumLength(1000).WithMessage("متن آدرس نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد");
        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("کد پستی الزامی است")
            .Length(10).WithMessage("کد پستی باید ۱۰ رقم باشد")
            .Matches("^[0-9]{10}$").WithMessage("کد پستی فقط شامل عدد است");
        RuleFor(x => x.Plate)
            .NotEmpty().WithMessage("پلاک الزامی است")
            .MaximumLength(20).WithMessage("پلاک نمی‌تواند بیشتر از ۲۰ کاراکتر باشد");
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("واحد الزامی است")
            .MaximumLength(20).WithMessage("واحد نمی‌تواند بیشتر از ۲۰ کاراکتر باشد");
        RuleFor(x => x.ReceiverName)
            .NotEmpty().WithMessage("نام تحویل‌گیرنده الزامی است")
            .MaximumLength(200).WithMessage("نام تحویل‌گیرنده نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");
        RuleFor(x => x.ReceiverPhone)
            .NotEmpty().WithMessage("شماره تحویل‌گیرنده الزامی است")
            .Matches("^09[0-9]{9}$").WithMessage("شماره تحویل‌گیرنده نامعتبر است");
    }
}
