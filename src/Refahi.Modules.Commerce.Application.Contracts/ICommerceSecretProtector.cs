namespace Refahi.Modules.Commerce.Application.Contracts;

public interface ICommerceSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
    string Hash(string plaintext);
}
