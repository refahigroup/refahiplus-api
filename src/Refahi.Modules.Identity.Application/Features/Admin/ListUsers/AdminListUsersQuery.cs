using System;
using System.Collections.Generic;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Admin.ListUsers;

public record AdminListUsersQuery(
    string? Search = null,
    string? Role = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<AdminUsersPagedResponse>;

public record AdminUsersPagedResponse(
    IEnumerable<AdminUserDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record AdminUserDto(
    Guid Id,
    string? MobileNumber,
    string? Email,
    string? Username,
    bool IsActive,
    bool MobileApproved,
    bool EmailApproved,
    DateTime? LockedUntil,
    DateTime CreatedAt,
    string? FirstName,
    string? LastName,
    IEnumerable<string> Roles);
