using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.CreateSupplier;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, CreateSupplierResponse>
{
    private readonly ISupplierRepository _repository;

    public CreateSupplierCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<CreateSupplierResponse> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            var exists = await _repository.ExistsByNationalIdAsync(request.NationalId, null, cancellationToken);
            if (exists)
                throw new SupplyChainDomainException("کد ملی/شناسه ملی تکراری است", "SUPPLIER_NATIONAL_ID_DUPLICATED");
        }

        var supplier = Supplier.Create(
            (SupplierType)request.Type,
            request.FirstName,
            request.LastName,
            request.CompanyName,
            request.BrandName,
            request.LogoUrl,
            request.NationalId,
            request.EconomicCode,
            request.ProvinceId,
            request.CityId,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.MobileNumber,
            request.PhoneNumber,
            request.RepresentativeName,
            request.RepresentativePhone);

        await _repository.AddAsync(supplier, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new CreateSupplierResponse(supplier.Id);
    }
}
