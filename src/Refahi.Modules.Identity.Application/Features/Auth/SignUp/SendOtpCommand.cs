using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

/// <summary>
/// Request to send OTP code for user signup/verification
/// </summary>
public record SendOtpCommand(
    string? MobileNumber,
    string? Email) : IRequest<SendOtpResult>;

public record SendOtpResult(
    bool Success,
    string? ErrorMessage = null,
    string? Token = null,
    DateTime? ExpiresAt = null);
