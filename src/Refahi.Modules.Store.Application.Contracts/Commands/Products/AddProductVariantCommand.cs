using MediatR;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddProductVariantCommand(
    Guid ProductId,
    List<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null
) : IRequest<AddProductVariantResponse>;

public sealed record VariantCombinationInput(Guid AttributeId, Guid ValueId);

public sealed record AddProductVariantResponse(Guid VariantId);
