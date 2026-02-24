using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Roles.RemoveRole;

/// <summary>
/// Remove a role from a user (Admin only)
/// </summary>
public record RemoveRoleCommand(
    Guid UserId,
    string Role) : IRequest<RemoveRoleResult>;

public record RemoveRoleResult(
    bool Success,
    string? ErrorMessage = null);
