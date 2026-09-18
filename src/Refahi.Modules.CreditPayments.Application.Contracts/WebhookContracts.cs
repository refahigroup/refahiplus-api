using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed record ResendWebhookEventCommand(Guid EventId, string? ActorId, string? Reason)
    : IRequest<ResendWebhookEventResponse>;

public sealed record ResendWebhookEventResponse(Guid EventId, string Status);

public sealed class WebhookOptions
{
    public const string SectionName = "CreditPayments:Webhooks";
    public int OutboxPollSeconds { get; set; } = 5;
    public int DeliveryPollSeconds { get; set; } = 5;
    public int ReconciliationPollSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaximumAttempts { get; set; } = 10;
    public int BaseRetrySeconds { get; set; } = 10;
    public int MaximumRetrySeconds { get; set; } = 3600;
    public int[] AllowedPorts { get; set; } = [443];
}
