using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierLinks.AddSupplierLink;

public class AddSupplierLinkCommandValidator : AbstractValidator<AddSupplierLinkCommand>
{
    public AddSupplierLinkCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");

        RuleFor(x => x.Type)
            .Must(t => Enum.IsDefined(typeof(SupplierLinkType), t))
            .WithMessage("نوع لینک معتبر نیست");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("آدرس لینک الزامی است")
            .MaximumLength(500).WithMessage("آدرس لینک نباید بیشتر از ۵۰۰ کاراکتر باشد")
            .Must(url => url.StartsWith("http://") || url.StartsWith("https://"))
            .WithMessage("آدرس لینک باید با http:// یا https:// شروع شود");
    }
}
