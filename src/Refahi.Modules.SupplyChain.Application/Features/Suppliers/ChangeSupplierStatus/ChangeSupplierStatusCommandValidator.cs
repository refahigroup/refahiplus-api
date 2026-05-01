using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.ChangeSupplierStatus;

public class ChangeSupplierStatusCommandValidator : AbstractValidator<ChangeSupplierStatusCommand>
{
    public ChangeSupplierStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");

        RuleFor(x => x.NewStatus)
            .Must(s => Enum.IsDefined(typeof(SupplierStatus), s))
            .WithMessage("وضعیت درخواستی معتبر نیست");

        When(x => (SupplierStatus)x.NewStatus == SupplierStatus.Rejected, () =>
        {
            RuleFor(x => x.Note)
                .NotEmpty().WithMessage("دلیل رد در هنگام رد کردن الزامی است");
        });
    }
}
