using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record AddProductImageCommand(
    Guid ProductId, string ImageUrl, bool IsMain, int SortOrder
) : IRequest<AddProductImageResponse>;

public sealed record AddProductImageResponse(int ImageId);
