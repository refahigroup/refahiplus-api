using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.CreateAgreement;

public class CreateAgreementCommandValidator : AbstractValidator<CreateAgreementCommand>
{
    public CreateAgreementCommandValidator()
    {
        RuleFor(x => x.AgreementNo)
            .NotEmpty().WithMessage("شماره قرارداد الزامی است")
            .MaximumLength(100).WithMessage("شماره قرارداد نباید بیشتر از ۱۰۰ کاراکتر باشد");

        RuleFor(x => x.Type)
            .Must(t => Enum.IsDefined(typeof(AgreementType), t))
            .WithMessage("نوع قرارداد معتبر نیست");

        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("تامین‌کننده الزامی است");

        RuleFor(x => x.FromDate)
            .NotEmpty().WithMessage("تاریخ شروع الزامی است");

        RuleFor(x => x.ToDate)
            .NotEmpty().WithMessage("تاریخ پایان الزامی است")
            .GreaterThan(x => x.FromDate).WithMessage("تاریخ پایان باید بعد از تاریخ شروع باشد");
    }
}
