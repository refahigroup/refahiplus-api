using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;

public sealed record GetCommerceSellersQuery() : IRequest<IReadOnlyList<CommerceSellerDto>>;

