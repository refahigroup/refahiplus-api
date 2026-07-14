using MediatR;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record UpdateProductVariantCommand(
    Guid ProductId,
    Guid VariantId,
    List<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null) : IRequest<Unit>;
