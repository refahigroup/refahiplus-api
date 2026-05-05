using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Features.Addresses.Mapping;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.AddAddress;

public class AddAddressCommandHandler : IRequestHandler<AddAddressCommand, UserAddressDto>
{
    private readonly IUserAddressRepository _repo;

    public AddAddressCommandHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<UserAddressDto> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        // تعیین خودکار IsDefault: اگر کاربر هیچ آدرسی ندارد، اولین آدرس را پیش‌فرض می‌کنیم
        var existing = await _repo.GetByUserIdAsync(request.UserId, cancellationToken);
        var makeDefault = request.IsDefault || existing.Count == 0;

        var address = UserAddress.Create(
            userId: request.UserId,
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
            longitude: request.Longitude,
            isDefault: makeDefault);

        if (makeDefault)
        {
            // برداشتن علامت پیش‌فرض از سایر آدرس‌ها
            await _repo.UnsetDefaultForUserAsync(request.UserId, exceptAddressId: address.Id, cancellationToken);
        }

        await _repo.AddAsync(address, cancellationToken);

        return address.ToDto();
    }
}
