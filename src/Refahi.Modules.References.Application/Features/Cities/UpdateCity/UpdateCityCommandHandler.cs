using MediatR;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Cities.UpdateCity;

public class UpdateCityCommandHandler : IRequestHandler<UpdateCityCommand, UpdateCityResponse>
{
    private readonly ICityRepository _cityRepository;

    public UpdateCityCommandHandler(ICityRepository cityRepository)
        => _cityRepository = cityRepository;

    public async Task<UpdateCityResponse> Handle(
        UpdateCityCommand request, CancellationToken cancellationToken)
    {
        var city = await _cityRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ReferencesDomainException("شهر یافت نشد", "CITY_NOT_FOUND");

        if (await _cityRepository.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), request.Id, cancellationToken))
            throw new ReferencesDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        city.UpdateInfo(request.Name, request.Slug, request.SortOrder);

        await _cityRepository.UpdateAsync(city, cancellationToken);

        return new UpdateCityResponse(city.Id, city.Name);
    }
}
