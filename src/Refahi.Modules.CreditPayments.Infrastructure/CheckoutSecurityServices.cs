using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class HmacCheckoutSecurityService(IOptions<CheckoutOptions> options) : ICheckoutSecurityService
{
    public CheckoutTokenMaterial CreateCheckoutToken(Guid checkoutSessionId,Guid gatewayPaymentId)
    {
        var version=options.Value.ActiveTokenKeyVersion;
        var nonce=RandomNumberGenerator.GetBytes(32);
        var tokenBytes=Derive(checkoutSessionId,gatewayPaymentId,nonce,version);
        return new(nonce,SHA256.HashData(tokenBytes),version,Encode(tokenBytes));
    }
    public string RecreateCheckoutToken(CheckoutSession checkoutSession)
    {
        var tokenBytes=Derive(checkoutSession.Id,checkoutSession.GatewayPaymentId,checkoutSession.TokenNonce,checkoutSession.TokenKeyVersion);
        var hash=SHA256.HashData(tokenBytes);
        if(!CryptographicOperations.FixedTimeEquals(hash,checkoutSession.TokenHash))
            throw new InvalidOperationException("Checkout token material integrity validation failed.");
        return Encode(tokenBytes);
    }
    public bool TryHashPresentedToken(string token,out byte[] tokenHash)
    {
        tokenHash=[];
        if(!TryDecode(token,out var bytes)||bytes.Length!=32)return false;
        tokenHash=SHA256.HashData(bytes);
        return true;
    }
    public CheckoutAuthorizationMaterial CreateAuthorizationSecret()
    {
        var bytes=RandomNumberGenerator.GetBytes(32);
        return new(Encode(bytes),SHA256.HashData(bytes));
    }
    public bool VerifyAuthorizationSecret(string secret,byte[] expectedHash)
    {
        if(expectedHash.Length!=32||!TryDecode(secret,out var bytes)||bytes.Length!=32)return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes),expectedHash);
    }
    private byte[] Derive(Guid sessionId,Guid paymentId,byte[] nonce,int version)
    {
        var key=GetKey(version);
        Span<byte> input=stackalloc byte[68];
        sessionId.TryWriteBytes(input[..16]);
        paymentId.TryWriteBytes(input.Slice(16,16));
        nonce.CopyTo(input.Slice(32,32));
        BinaryPrimitives.WriteInt32BigEndian(input[64..],version);
        return HMACSHA256.HashData(key,input);
    }
    private byte[] GetKey(int version)
    {
        if(version<=0||!options.Value.TokenKeys.TryGetValue(version,out var configured)||string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException($"Checkout token key version {version} is not configured.");
        try
        {
            var key=Convert.FromBase64String(configured);
            if(key.Length<32)throw new InvalidOperationException("Checkout token keys must contain at least 256 bits.");
            return key;
        }
        catch(FormatException ex){throw new InvalidOperationException("Checkout token key must be Base64 encoded.",ex);}
    }
    private static string Encode(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static bool TryDecode(string value,out byte[] bytes)
    {
        bytes=[];
        if(string.IsNullOrWhiteSpace(value)||value.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))return false;
        var normalized=value.Replace('-','+').Replace('_','/');
        normalized+=new string('=',(4-normalized.Length%4)%4);
        try
        {
            bytes=Convert.FromBase64String(normalized);
            var canonical=Encode(bytes);
            var presented=System.Text.Encoding.ASCII.GetBytes(value);
            var expected=System.Text.Encoding.ASCII.GetBytes(canonical);
            return presented.Length==expected.Length&&CryptographicOperations.FixedTimeEquals(presented,expected);
        }
        catch(FormatException){return false;}
    }
}

public sealed class DataProtectionCheckoutOtpReferenceProtector(IDataProtectionProvider provider)
    : ICheckoutOtpReferenceProtector
{
    private readonly IDataProtector protector=provider.CreateProtector("Refahi.CreditPayments.CheckoutOtpReference.v1");
    public string Protect(string referenceCode)=>protector.Protect(referenceCode);
    public bool TryUnprotect(string protectedReferenceCode,out string referenceCode)
    {
        referenceCode=string.Empty;
        if(string.IsNullOrWhiteSpace(protectedReferenceCode))return false;
        try{referenceCode=protector.Unprotect(protectedReferenceCode);return !string.IsNullOrWhiteSpace(referenceCode);}
        catch(CryptographicException){return false;}
    }
}
