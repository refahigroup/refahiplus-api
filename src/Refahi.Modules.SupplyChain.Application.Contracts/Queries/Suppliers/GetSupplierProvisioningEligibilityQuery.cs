using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

public sealed record GetSupplierProvisioningEligibilityQuery(Guid SupplierId):IRequest<SupplierProvisioningEligibilityDto?>;
public sealed record SupplierProvisioningEligibilityDto(Guid SupplierId,string DisplayName,short Status,bool IsApproved,bool IsTerminalLifecycleState);
