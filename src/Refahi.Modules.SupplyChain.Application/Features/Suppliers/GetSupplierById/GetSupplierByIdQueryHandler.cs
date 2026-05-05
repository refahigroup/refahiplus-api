using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.GetSupplierById;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly ISupplierRepository _repository;
    private readonly IPathService _pathService;

    public GetSupplierByIdQueryHandler(ISupplierRepository repository, IPathService pathService)
    {
        _repository = repository;
        _pathService = pathService;
    }

    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, true, cancellationToken);

        if (supplier is null || supplier.IsDeleted)
            return null;

        return MapToDto(supplier, _pathService);
    }

    private static SupplierDto MapToDto(Supplier s, IPathService pathService) => new(
        s.Id,
        (short)s.Type,
        s.Type.ToString(),
        s.FirstName,
        s.LastName,
        s.CompanyName,
        s.BrandName,
        s.LogoUrl is null ? null : pathService.MakeAbsoluteMediaUrl(s.LogoUrl),
        s.NationalId,
        s.EconomicCode,
        s.ProvinceId,
        s.CityId,
        s.Address,
        s.Latitude,
        s.Longitude,
        s.MobileNumber,
        s.PhoneNumber,
        s.RepresentativeName,
        s.RepresentativePhone,
        (short)s.Status,
        s.Status.ToString(),
        s.StatusNote,
        s.CreatedAt,
        s.UpdatedAt,
        s.Links.Select(l => new SupplierLinkDto(l.Id, (short)l.Type, l.Type.ToString(), l.Url, l.Label, l.CreatedAt)).ToList().AsReadOnly(),
        s.Attachments.Select(a => new SupplierAttachmentDto(a.Id, a.Title, a.FileUrl, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt)).ToList().AsReadOnly());
}
