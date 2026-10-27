namespace Refahi.Modules.Entertainments.Application.Contracts;

public interface IEntertainmentSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
    string Hash(string plaintext);
}
