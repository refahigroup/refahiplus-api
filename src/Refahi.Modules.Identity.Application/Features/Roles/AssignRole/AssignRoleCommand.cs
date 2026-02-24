using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Roles.AssignRole;

/// <summary>
/// Assign a role to a user (Admin only)
/// </summary>
public record AssignRoleCommand(
    Guid UserId,
    string Role,
    Guid? AssignedBy = null) : IRequest<AssignRoleResult>;

public record AssignRoleResult(
    bool Success,
    string? ErrorMessage = null);
