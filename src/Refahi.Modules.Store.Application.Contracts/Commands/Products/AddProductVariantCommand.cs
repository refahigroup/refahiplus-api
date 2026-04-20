using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddProductVariantCommand(
    Guid ProductId,
    List<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    string? Sku
) : IRequest<AddProductVariantResponse>;

public sealed record VariantCombinationInput(Guid AttributeId, Guid ValueId);

public sealed record AddProductVariantResponse(Guid VariantId);
