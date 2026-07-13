using FluentValidation;
using Refahi.Modules.Identity.Application.Features.Addresses;

namespace Refahi.Modules.Identity.Application.Features.Addresses.UpdateAddress;

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر نامعتبر است");
        Include(new AddressInputValidator<UpdateAddressCommand>());
    }
}
