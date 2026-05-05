using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Features.Addresses.Mapping;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.SetDefaultAddress;

public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, UserAddressDto>
{
    private readonly IUserAddressRepository _repo;

    public SetDefaultAddressCommandHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<UserAddressDto> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _repo.GetByIdForUserAsync(request.AddressId, request.UserId, cancellationToken)
            ?? throw new DomainException("آدرس یافت نشد", "ADDRESS_NOT_FOUND");

        if (!address.IsDefault)
        {
            // ابتدا سایر آدرس‌های پیش‌فرض کاربر را بردارید
            await _repo.UnsetDefaultForUserAsync(request.UserId, exceptAddressId: address.Id, cancellationToken);
            address.MarkAsDefault();
            await _repo.UpdateAsync(address, cancellationToken);
        }

        return address.ToDto();
    }
}
