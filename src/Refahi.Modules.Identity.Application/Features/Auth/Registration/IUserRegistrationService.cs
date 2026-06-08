using System.Threading;
using System.Threading.Tasks;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Auth.Registration;

public interface IUserRegistrationService
{
    Task<RegistrationResult> RegisterAsync(
        string? mobileNumber,
        string? email,
        CancellationToken cancellationToken);
}

public sealed record RegistrationResult(
    bool Success,
    string? ErrorMessage = null,
    UserDto? User = null,
    string? MobileNumber = null,
    string? Email = null);
