using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Identity.Application.Features.Addresses;

internal static class AddressLocationValidation
{
    public static async Task EnsureValidAsync(
        IMediator mediator,
        int provinceId,
        int cityId,
        CancellationToken ct)
    {
        var city = await mediator.Send(new GetCityByIdQuery(cityId), ct);
        if (city is null || !city.IsActive)
            throw new DomainException("شهر انتخاب‌شده معتبر نیست", "INVALID_CITY");

        if (city.ProvinceId != provinceId)
            throw new DomainException("شهر انتخاب‌شده متعلق به استان انتخاب‌شده نیست", "CITY_PROVINCE_MISMATCH");
    }
}
