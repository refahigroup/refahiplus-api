namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;

public sealed class CancelProviderBookingResultDto
{
    public string Status { get; set; } = "Unsupported";
    public string? ProviderMessage { get; set; }

    public bool IsCancelled =>
        Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("AlreadyCancelled", StringComparison.OrdinalIgnoreCase);

    public bool IsUnsupported =>
        Status.Equals("Unsupported", StringComparison.OrdinalIgnoreCase);
}
