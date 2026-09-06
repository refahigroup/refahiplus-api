using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;

public sealed record GetCommerceSellersQuery(int PageNumber = 1, int PageSize = 24, string? ProviderKey = null,
    string? LocationCode = null, string? CategoryCode = null) : IRequest<CommerceSellerPage>;

