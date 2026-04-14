using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Sessions;

public sealed record UpdateSessionCommand(
    Guid SessionId,
    int Capacity,
    string? Title,
    long PriceAdjustment,
    bool IsActive
) : IRequest<UpdateSessionResponse>;

public sealed record UpdateSessionResponse(Guid SessionId);
