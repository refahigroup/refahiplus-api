namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public static class EntertainmentAdmission
{
    // Never turn an internal ticket id or an external web page into an admission credential.
    public static string? Credential(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048
        && !value.Any(char.IsControl) && !Uri.TryCreate(value, UriKind.Absolute, out _) && !value.StartsWith("//", StringComparison.Ordinal)
        ? value : null;
    public static string? Code(EntertainmentIssuedTicket ticket) => Credential(ticket.AdmissionCode ??
        (ticket.Code != ticket.ProviderTicketId ? ticket.Code : null));
    public static bool Ready(EntertainmentIssuedTicket ticket) => Code(ticket) != null || Credential(ticket.QrCode) != null;
}
