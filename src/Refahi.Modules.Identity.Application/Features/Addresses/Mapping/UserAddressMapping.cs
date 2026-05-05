using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Application.Features.Addresses.Mapping;

internal static class UserAddressMapping
{
    public static UserAddressDto ToDto(this UserAddress a) =>
        new(a.Id, a.UserId, a.Title, a.ProvinceId, a.CityId, a.FullAddress, a.PostalCode,
            a.ReceiverName, a.ReceiverPhone, a.Plate, a.Unit, a.Latitude, a.Longitude,
            a.IsDefault, a.CreatedAt, a.UpdatedAt);
}
