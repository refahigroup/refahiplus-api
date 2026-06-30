using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CancelProviderBooking;

public sealed record CancelProviderBookingCommand(
    Guid SagaId,
    string Reason,
    string? IdempotencyKey = null) : IRequest<CancelProviderBookingResponse>;

public sealed record CancelProviderBookingResponse(
    Guid SagaId,
    Guid HotelRequestId,
    string? ProviderBookingCode,
    string Outcome,
    bool CancellationAttempted,
    bool ExternalUnresolved);
