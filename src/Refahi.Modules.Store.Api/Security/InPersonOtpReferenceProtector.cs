using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Store.Application.Contracts.Vendor;

namespace Refahi.Modules.Store.Api.Security;

internal sealed class InPersonOtpReferenceProtector : IInPersonOtpReferenceProtector
{
    private readonly IDataProtector _protector;

    public InPersonOtpReferenceProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Refahi.Store.VendorInPersonOtp.v1");
    }

    public string Protect(InPersonOtpReference reference) =>
        _protector.Protect(JsonSerializer.Serialize(reference));

    public bool TryUnprotect(string protectedReference, out InPersonOtpReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(protectedReference))
            return false;
        try
        {
            reference = JsonSerializer.Deserialize<InPersonOtpReference>(
                _protector.Unprotect(protectedReference)
            );
            return reference is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
