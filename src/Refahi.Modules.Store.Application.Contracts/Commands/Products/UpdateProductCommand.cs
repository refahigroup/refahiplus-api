using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record UpdateProductCommand(
    Guid Id,
    string Title,
    long PriceMinor,
    long? DiscountedPriceMinor,
    int? DiscountPercent,
    string? Description,
    int? CityId,
    string? Area,
    bool IsAvailable
) : IRequest<UpdateProductResponse>;

public sealed record UpdateProductResponse(Guid Id, string Title);
