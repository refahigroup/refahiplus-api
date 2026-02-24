using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Profile.GetProfile;

/// <summary>
/// Get user profile by user ID
/// </summary>
public record GetProfileQuery(Guid UserId) : IRequest<GetProfileResult>;

public record GetProfileResult(
    bool Success,
    string? ErrorMessage = null,
    UserProfileDto? Profile = null);
