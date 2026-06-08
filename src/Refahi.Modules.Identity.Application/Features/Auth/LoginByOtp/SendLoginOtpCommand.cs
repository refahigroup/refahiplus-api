using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public record SendLoginOtpCommand(string Contact) : IRequest<SendLoginOtpResult>;

public record SendLoginOtpResult(
    bool Success,
    string? ErrorMessage = null,
    string? Token = null,
    DateTime? ExpiresAt = null,
    string? Flow = null);
