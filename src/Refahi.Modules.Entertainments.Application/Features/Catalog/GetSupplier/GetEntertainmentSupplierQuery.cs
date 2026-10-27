using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;

public sealed record GetEntertainmentSupplierQuery(string ProviderKey, string SupplierKey)
    : IRequest<EntertainmentSupplierDto?>;
