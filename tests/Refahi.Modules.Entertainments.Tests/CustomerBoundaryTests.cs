using System.Text.Json;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Xunit;
namespace Refahi.Modules.Entertainments.Tests;
public sealed class CustomerBoundaryTests
{
    [Fact]
    public void Internal_ticket_identity_and_external_documents_are_not_admission_credentials()
    {
        Assert.False(EntertainmentAdmission.Ready(new("internal-id", false) { ProviderTicketId = "internal-id" }));
        Assert.False(EntertainmentAdmission.Ready(new("internal-id", false) { ProviderTicketId = "internal-id", QrCode = "https://supplier.test/ticket" }));
        var ticket = new EntertainmentIssuedTicket("internal-id", false) { ProviderTicketId = "internal-id", AdmissionCode = "123456", QrCode = "exact-admission-payload" };
        Assert.True(EntertainmentAdmission.Ready(ticket)); Assert.Equal("123456", EntertainmentAdmission.Code(ticket));
        Assert.Equal("exact-admission-payload", EntertainmentAdmission.Credential(ticket.QrCode));
    }
    [Fact]
    public void Historical_order_metadata_uses_an_allowlist_and_retains_the_program_identity()
    {
        var value = EntertainmentPublicMetadata.Project("""{"ProviderKey":"touristpanel","SupplierKey":"native","ProgramKey":"public-id","secret":"private"}""");
        using var json = JsonDocument.Parse(value!);
        Assert.Equal("public-id", json.RootElement.GetProperty("ProgramKey").GetString());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Null(EntertainmentPublicMetadata.Project("invalid"));
        Assert.Equal("خدمت", EntertainmentPresentation.Text("TouristPanel خدمت"));
    }
}
