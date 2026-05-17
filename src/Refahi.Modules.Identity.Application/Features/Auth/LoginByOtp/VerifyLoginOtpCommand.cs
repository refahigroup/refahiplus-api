using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public record VerifyLoginOtpCommand(
    string Token,
    string OtpCode) : IRequest<VerifyLoginOtpResult>;

public record VerifyLoginOtpResult(
    bool Success,
    string? ErrorMessage = null,
    UserDto? User = null);
