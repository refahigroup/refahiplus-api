using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Application.Features.Profile.CreateOrUpdate;

/// <summary>
/// Create or Update user profile
/// </summary>
public record CreateOrUpdateProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? NationalCode = null,
    Gender? Gender = null) : IRequest<CreateOrUpdateProfileResult>;

public record CreateOrUpdateProfileResult(
    bool Success,
    string? ErrorMessage = null,
    UserProfileDto? Profile = null);
