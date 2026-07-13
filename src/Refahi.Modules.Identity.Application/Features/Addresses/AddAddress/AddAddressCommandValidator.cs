using FluentValidation;
using Refahi.Modules.Identity.Application.Features.Addresses;

namespace Refahi.Modules.Identity.Application.Features.Addresses.AddAddress;

public class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر نامعتبر است");
        Include(new AddressInputValidator<AddAddressCommand>());
    }
}
