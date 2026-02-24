using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Profile.GetProfile;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, GetProfileResult>
{
    private readonly IUserProfileRepository _profileRepository;

    public GetProfileQueryHandler(IUserProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public async Task<GetProfileResult> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (profile == null)
        {
            return new GetProfileResult(false, "Profile not found");
        }

        var profileDto = new UserProfileDto(
            profile.Id,
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.NationalCode,
            profile.Gender);

        return new GetProfileResult(true, null, profileDto);
    }
}
