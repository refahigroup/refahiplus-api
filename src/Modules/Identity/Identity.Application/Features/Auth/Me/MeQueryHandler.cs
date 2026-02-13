using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Contracts.Models;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.Me;

public class MeQueryHandler : IRequestHandler<MeQuery, UserDto>
{
    private readonly IUserRepository _auth;

    public MeQueryHandler(IUserRepository auth)
    {
        _auth = auth;
    }

    public async Task<UserDto> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var user = await _auth.GetByIdAsync(request.UserId);

        return (user != null)
            ? new UserDto(user.Id, user.Username, user.Roles)
            : null;
    }
}
