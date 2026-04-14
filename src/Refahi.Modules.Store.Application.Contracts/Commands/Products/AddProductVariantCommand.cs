using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddProductVariantCommand(
    Guid ProductId, string? Size, string? Color, string? ColorHex,
    string? ImageUrl, int StockCount, long PriceAdjustment
) : IRequest<AddProductVariantResponse>;

public sealed record AddProductVariantResponse(Guid VariantId);
