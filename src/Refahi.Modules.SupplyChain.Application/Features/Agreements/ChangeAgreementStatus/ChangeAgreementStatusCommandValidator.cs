using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.ChangeAgreementStatus;

public class ChangeAgreementStatusCommandValidator : AbstractValidator<ChangeAgreementStatusCommand>
{
    public ChangeAgreementStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("شناسه قرارداد الزامی است");

        RuleFor(x => x.NewStatus)
            .Must(s => Enum.IsDefined(typeof(AgreementStatus), s))
            .WithMessage("وضعیت درخواستی معتبر نیست");

        When(x => (AgreementStatus)x.NewStatus == AgreementStatus.Rejected, () =>
        {
            RuleFor(x => x.Note)
                .NotEmpty().WithMessage("دلیل رد در هنگام رد کردن الزامی است");
        });
    }
}
