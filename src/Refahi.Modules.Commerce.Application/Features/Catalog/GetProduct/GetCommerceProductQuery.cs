using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetProduct;

public sealed record GetCommerceProductQuery(
    string ProviderKey, 
    string ProductKey
) : IRequest<CommerceProductDto?>;

