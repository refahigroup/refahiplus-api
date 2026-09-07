using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSeller;

public sealed record GetCommerceSellerQuery(string ProviderKey, string SellerKey)
    : IRequest<CommerceSellerDto?>;
