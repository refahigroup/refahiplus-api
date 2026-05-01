using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.UpdateSupplier;

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Unit>
{
    private readonly ISupplierRepository _repository;

    public UpdateSupplierCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            var exists = await _repository.ExistsByNationalIdAsync(request.NationalId, request.Id, cancellationToken);
            if (exists)
                throw new SupplyChainDomainException("کد ملی/شناسه ملی تکراری است", "SUPPLIER_NATIONAL_ID_DUPLICATED");
        }

        supplier.UpdateProfile(
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

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
