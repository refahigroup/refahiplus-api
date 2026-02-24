using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.Me;

public class MeQueryHandler : IRequestHandler<MeQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public MeQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
            return null;

        return new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());
    }
}
