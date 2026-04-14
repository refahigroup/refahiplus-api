using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Sessions;

public sealed record CreateSessionCommand(
    Guid ProductId,
    string Date,
    string StartTime,
    string EndTime,
    int Capacity,
    string? Title,
    long PriceAdjustment
) : IRequest<CreateSessionResponse>;

public sealed record CreateSessionResponse(Guid SessionId, string Date, string StartTime, string EndTime, int Capacity);
