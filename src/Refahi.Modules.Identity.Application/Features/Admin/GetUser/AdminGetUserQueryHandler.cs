using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Admin.GetUser;

public class AdminGetUserQueryHandler : IRequestHandler<AdminGetUserQuery, AdminUserDetailDto?>
{
    private readonly IUserRepository _userRepository;

    public AdminGetUserQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<AdminUserDetailDto?> Handle(AdminGetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return null;

        return new AdminUserDetailDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.Username,
            user.IsActive,
            user.MobileApproved,
            user.EmailApproved,
            user.LockedUntil,
            user.CreatedAt,
            user.Profile?.FirstName,
            user.Profile?.LastName,
            user.Profile?.NationalCode,
            user.Profile?.ProfileImageUrl,
            user.Roles.Select(r => r.Role));
    }
}
