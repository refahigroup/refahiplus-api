using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record ReorderProductImagesCommand(
    Guid ProductId,
    List<ProductImageOrderItem> Items
) : IRequest<Unit>;

public sealed record ProductImageOrderItem(int ImageId, int SortOrder);
