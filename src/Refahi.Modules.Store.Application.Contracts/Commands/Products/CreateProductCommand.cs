using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record CreateProductCommand(
    Guid ShopId,
    string Title,
    string Slug,
    long PriceMinor,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    int CategoryId,
    string CategoryCode,
    decimal CommissionPercent,
    string? Description,
    int StockCount,
    int? CityId,
    string? Area
) : IRequest<CreateProductResponse>;

public sealed record CreateProductResponse(Guid Id, string Title, string Slug);
