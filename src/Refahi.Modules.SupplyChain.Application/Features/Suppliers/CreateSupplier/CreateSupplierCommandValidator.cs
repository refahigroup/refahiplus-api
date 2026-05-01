using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.CreateSupplier;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => Enum.IsDefined(typeof(SupplierType), t))
            .WithMessage("نوع تامین‌کننده معتبر نیست");

        When(x => (SupplierType)x.Type == SupplierType.Individual, () =>
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("نام الزامی است")
                .MaximumLength(100).WithMessage("نام نباید بیشتر از ۱۰۰ کاراکتر باشد");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("نام خانوادگی الزامی است")
                .MaximumLength(100).WithMessage("نام خانوادگی نباید بیشتر از ۱۰۰ کاراکتر باشد");

            When(x => !string.IsNullOrWhiteSpace(x.NationalId), () =>
            {
                RuleFor(x => x.NationalId)
                    .Matches(@"^\d{10}$").WithMessage("کد ملی باید ۱۰ رقم عددی باشد");
            });
        });

        When(x => (SupplierType)x.Type == SupplierType.Legal, () =>
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("نام شرکت الزامی است")
                .MaximumLength(200).WithMessage("نام شرکت نباید بیشتر از ۲۰۰ کاراکتر باشد");

            When(x => !string.IsNullOrWhiteSpace(x.NationalId), () =>
            {
                RuleFor(x => x.NationalId)
                    .Matches(@"^\d{11}$").WithMessage("شناسه ملی باید ۱۱ رقم عددی باشد");
            });
        });

        When(x => !string.IsNullOrWhiteSpace(x.BrandName), () =>
        {
            RuleFor(x => x.BrandName).MaximumLength(200).WithMessage("نام برند نباید بیشتر از ۲۰۰ کاراکتر باشد");
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
