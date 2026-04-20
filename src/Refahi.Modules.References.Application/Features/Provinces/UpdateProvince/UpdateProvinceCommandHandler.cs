using MediatR;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Provinces.UpdateProvince;

public class UpdateProvinceCommandHandler : IRequestHandler<UpdateProvinceCommand, UpdateProvinceResponse>
{
    private readonly IProvinceRepository _provinceRepository;

    public UpdateProvinceCommandHandler(IProvinceRepository provinceRepository)
        => _provinceRepository = provinceRepository;

    public async Task<UpdateProvinceResponse> Handle(
        UpdateProvinceCommand request, CancellationToken cancellationToken)
    {
        var province = await _provinceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ReferencesDomainException("استان یافت نشد", "PROVINCE_NOT_FOUND");

        if (await _provinceRepository.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), request.Id, cancellationToken))
            throw new ReferencesDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        province.UpdateInfo(request.Name, request.Slug, request.SortOrder);

        await _provinceRepository.UpdateAsync(province, cancellationToken);

        return new UpdateProvinceResponse(province.Id, province.Name);
    }
}
