using MediatR;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Cities.CreateCity;

public class CreateCityCommandHandler : IRequestHandler<CreateCityCommand, CreateCityResponse>
{
    private readonly ICityRepository _cityRepository;
    private readonly IProvinceRepository _provinceRepository;

    public CreateCityCommandHandler(ICityRepository cityRepository, IProvinceRepository provinceRepository)
    {
        _cityRepository = cityRepository;
        _provinceRepository = provinceRepository;
    }

    public async Task<CreateCityResponse> Handle(
        CreateCityCommand request, CancellationToken cancellationToken)
    {
        var province = await _provinceRepository.GetByIdAsync(request.ProvinceId, cancellationToken)
            ?? throw new ReferencesDomainException("استان یافت نشد", "PROVINCE_NOT_FOUND");

        if (await _cityRepository.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), request.ProvinceId, null, cancellationToken))
            throw new ReferencesDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        var city = City.Create(request.Name, request.Slug, request.ProvinceId, request.SortOrder);

        await _cityRepository.AddAsync(city, cancellationToken);

        return new CreateCityResponse(city.Id, city.Name);
    }
}
