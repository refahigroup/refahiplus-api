using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public LoginCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByMobileOrEmailAsync(request.MobileOrEmail, cancellationToken);

        if (user == null)
            return null;

        if (!user.IsActive)
            return null;

        if (!user.VerifyPassword(request.Password))
            return null;

        return new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());
    }
}

