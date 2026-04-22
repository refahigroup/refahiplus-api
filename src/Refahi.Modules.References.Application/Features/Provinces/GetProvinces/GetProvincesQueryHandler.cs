using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Provinces.GetProvinces;

public class GetProvincesQueryHandler : IRequestHandler<GetProvincesQuery, GetProvincesResponse>
{
    private readonly IProvinceRepository _provinceRepository;

    public GetProvincesQueryHandler(IProvinceRepository provinceRepository)
        => _provinceRepository = provinceRepository;

    public async Task<GetProvincesResponse> Handle(
        GetProvincesQuery request, CancellationToken cancellationToken)
    {
        var provinces = await _provinceRepository.GetAllAsync(request.ActiveOnly, cancellationToken);

        var dtos = provinces.Select(p => new ProvinceDto(
            p.Id,
            p.Name,
            p.NameEn,
            p.Slug,
            p.SortOrder,
            p.IsActive
        )).ToList();

        return new GetProvincesResponse(dtos);
    }
}
