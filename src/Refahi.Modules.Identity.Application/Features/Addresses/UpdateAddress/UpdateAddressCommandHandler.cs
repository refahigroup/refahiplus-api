using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Features.Addresses.Mapping;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.UpdateAddress;

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, UserAddressDto>
{
    private readonly IUserAddressRepository _repo;

    public UpdateAddressCommandHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<UserAddressDto> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _repo.GetByIdForUserAsync(request.AddressId, request.UserId, cancellationToken)
            ?? throw new DomainException("آدرس یافت نشد", "ADDRESS_NOT_FOUND");

        address.Update(
            title: request.Title,
            provinceId: request.ProvinceId,
            cityId: request.CityId,
            fullAddress: request.FullAddress,
            postalCode: request.PostalCode,
            receiverName: request.ReceiverName,
            receiverPhone: request.ReceiverPhone,
            plate: request.Plate,
            unit: request.Unit,
            latitude: request.Latitude,
            longitude: request.Longitude);

        await _repo.UpdateAsync(address, cancellationToken);
        return address.ToDto();
    }
}
