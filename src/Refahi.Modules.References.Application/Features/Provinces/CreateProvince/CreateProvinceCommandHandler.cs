using MediatR;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Provinces.CreateProvince;

public class CreateProvinceCommandHandler : IRequestHandler<CreateProvinceCommand, CreateProvinceResponse>
{
    private readonly IProvinceRepository _provinceRepository;

    public CreateProvinceCommandHandler(IProvinceRepository provinceRepository)
        => _provinceRepository = provinceRepository;

    public async Task<CreateProvinceResponse> Handle(
        CreateProvinceCommand request, CancellationToken cancellationToken)
    {
        if (await _provinceRepository.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), null, cancellationToken))
            throw new ReferencesDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        var province = Province.Create(request.Name, request.Slug, request.SortOrder);

        await _provinceRepository.AddAsync(province, cancellationToken);

        return new CreateProvinceResponse(province.Id, province.Name);
    }
}
