using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;
public sealed record GetEntertainmentSupplierQuery(Guid SupplierKey) : IRequest<EntertainmentSupplierDto?>;
