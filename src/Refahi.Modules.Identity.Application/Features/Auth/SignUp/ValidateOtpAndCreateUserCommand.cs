using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

/// <summary>
/// Validates OTP and creates a new user account
/// </summary>
public record ValidateOtpAndCreateUserCommand(
    string Token,
    string OtpCode) : IRequest<ValidateOtpResult>;

public record ValidateOtpResult(
    bool Success,
    string? ErrorMessage = null,
    UserDto? User = null,
    string? MobileNumber = null,
    string? Email = null);
