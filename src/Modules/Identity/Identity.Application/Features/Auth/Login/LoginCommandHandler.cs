using Identity.Application.Contracts.Models;
using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, UserDto>
{
    private readonly IUserRepository _auth;

    public LoginCommandHandler(IUserRepository auth)
    {
        _auth = auth;
    }

    public async Task<UserDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _auth.GetByUsername(request.Username);

        return user.VerifyPassword(request.Password)
            ? new UserDto(user.Id, user.Username, user.Roles) 
            : null;
    }
}
