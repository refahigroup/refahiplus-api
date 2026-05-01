using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.UpdateSupplier;

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");

        When(x => !string.IsNullOrWhiteSpace(x.FirstName), () =>
        {
            RuleFor(x => x.FirstName).MaximumLength(100).WithMessage("نام نباید بیشتر از ۱۰۰ کاراکتر باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.LastName), () =>
        {
            RuleFor(x => x.LastName).MaximumLength(100).WithMessage("نام خانوادگی نباید بیشتر از ۱۰۰ کاراکتر باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.CompanyName), () =>
        {
            RuleFor(x => x.CompanyName).MaximumLength(200).WithMessage("نام شرکت نباید بیشتر از ۲۰۰ کاراکتر باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.BrandName), () =>
        {
            RuleFor(x => x.BrandName).MaximumLength(200).WithMessage("نام برند نباید بیشتر از ۲۰۰ کاراکتر باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.NationalId), () =>
        {
            RuleFor(x => x.NationalId)
                .Must(n => System.Text.RegularExpressions.Regex.IsMatch(n!, @"^\d{10}$") ||
                           System.Text.RegularExpressions.Regex.IsMatch(n!, @"^\d{11}$"))
                .WithMessage("کد ملی باید ۱۰ رقم یا شناسه ملی ۱۱ رقم عددی باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.EconomicCode), () =>
        {
            RuleFor(x => x.EconomicCode)
                .Matches(@"^\d{10,14}$").WithMessage("کد اقتصادی باید بین ۱۰ تا ۱۴ رقم عددی باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.MobileNumber), () =>
        {
            RuleFor(x => x.MobileNumber)
                .Matches(@"^09\d{9}$").WithMessage("شماره موبایل باید با ۰۹ شروع شده و ۱۱ رقم باشد");
        });

        When(x => x.Latitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude!.Value)
                .InclusiveBetween(-90, 90).WithMessage("عرض جغرافیایی باید بین -۹۰ و ۹۰ باشد");
        });

        When(x => x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Longitude!.Value)
                .InclusiveBetween(-180, 180).WithMessage("طول جغرافیایی باید بین -۱۸۰ و ۱۸۰ باشد");
        });

        When(x => !string.IsNullOrWhiteSpace(x.LogoUrl), () =>
        {
            RuleFor(x => x.LogoUrl)
                .MaximumLength(500).WithMessage("آدرس لوگو نباید بیشتر از ۵۰۰ کاراکتر باشد")
                .Matches(@"^https?://").WithMessage("آدرس لوگو باید با http یا https شروع شود");
        });
    }
}
