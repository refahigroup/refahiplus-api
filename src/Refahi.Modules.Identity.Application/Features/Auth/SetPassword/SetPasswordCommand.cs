using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Auth.SetPassword;

/// <summary>
/// Command to set password for a user (after signup via OTP)
/// </summary>
public record SetPasswordCommand(
    string MobileOrEmail,
    string Password) : IRequest<SetPasswordResult>;

public record SetPasswordResult(
    bool Success,
    string? ErrorMessage = null);
