using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierAttachments.AddSupplierAttachment;

public class AddSupplierAttachmentCommandValidator : AbstractValidator<AddSupplierAttachmentCommand>
{
    public AddSupplierAttachmentCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان پیوست الزامی است")
            .MaximumLength(150).WithMessage("عنوان پیوست نباید بیشتر از ۱۵۰ کاراکتر باشد");

        RuleFor(x => x.FileUrl)
            .NotEmpty().WithMessage("آدرس فایل الزامی است")
            .MaximumLength(1000).WithMessage("آدرس فایل نباید بیشتر از ۱۰۰۰ کاراکتر باشد")
            .Must(url => url.StartsWith("http://") || url.StartsWith("https://"))
            .WithMessage("آدرس فایل باید با http:// یا https:// شروع شود");
    }
}
