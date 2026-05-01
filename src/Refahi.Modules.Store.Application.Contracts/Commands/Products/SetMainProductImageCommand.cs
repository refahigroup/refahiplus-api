using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Products;

public sealed record SetMainProductImageCommand(Guid ProductId, int ImageId) : IRequest<Unit>;
