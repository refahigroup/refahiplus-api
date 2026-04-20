using System;
using System.Collections.Generic;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Admin.GetUser;

public record AdminGetUserQuery(Guid UserId) : IRequest<AdminUserDetailDto?>;

public record AdminUserDetailDto(
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
    string? NationalCode,
    string? ProfileImageUrl,
    IEnumerable<string> Roles);
