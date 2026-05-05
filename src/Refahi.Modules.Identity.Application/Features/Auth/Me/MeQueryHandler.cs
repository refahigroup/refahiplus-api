using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.Me;

public class MeQueryHandler : IRequestHandler<MeQuery, MeDetailDto?>
{
    private readonly IUserRepository _userRepository;

    public MeQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<MeDetailDto?> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return null;

        UserProfileDto? profileDto = null;
        if (user.Profile is not null)
        {
            profileDto = new UserProfileDto(
                user.Profile.Id,
                user.Profile.UserId,
                user.Profile.FirstName,
                user.Profile.LastName,
                user.Profile.NationalCode,
                user.Profile.Gender);
        }

        return new MeDetailDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles(),
            profileDto);
    }
}
