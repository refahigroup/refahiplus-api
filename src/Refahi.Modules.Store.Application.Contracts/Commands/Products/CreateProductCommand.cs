using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record CreateProductCommand(
    Guid AgreementProductId,
    string Title,
    string Slug,
    string? Description,
    int StockCount
) : IRequest<CreateProductResponse>;

public sealed record CreateProductResponse(Guid Id, string Title, string Slug);
