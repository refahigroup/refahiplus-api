using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Admin.ListUsers;

public class AdminListUsersQueryHandler : IRequestHandler<AdminListUsersQuery, AdminUsersPagedResponse>
{
    private readonly IUserRepository _userRepository;

    public AdminListUsersQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<AdminUsersPagedResponse> Handle(AdminListUsersQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _userRepository.GetPagedAsync(
            request.Search, request.Role, request.IsActive,
            request.PageNumber, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        var dtos = items.Select(u => new AdminUserDto(
            u.Id,
            u.MobileNumber,
            u.Email,
            u.Username,
            u.IsActive,
            u.MobileApproved,
            u.EmailApproved,
            u.LockedUntil,
            u.CreatedAt,
            u.Profile?.FirstName,
            u.Profile?.LastName,
            u.Roles.Select(r => r.Role)));

        return new AdminUsersPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
