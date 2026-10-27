using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Entertainments.Application.Contracts;

namespace Refahi.Modules.Entertainments.Infrastructure;

public sealed class EntertainmentSecretProtector(IDataProtectionProvider provider) : IEntertainmentSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Refahi.Entertainment.Secrets.v1");
    public string Protect(string plaintext) => 
        protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => 
        protector.Unprotect(protectedValue);

    public string Hash(string plaintext) => 
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
}
