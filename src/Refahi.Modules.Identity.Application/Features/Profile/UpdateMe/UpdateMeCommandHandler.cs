using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Features.Profile.UpdateMe;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Profile.UpdateMe;

public class UpdateMeCommandHandler : IRequestHandler<UpdateMeCommand, UpdateMeResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserProfileRepository _profileRepository;

    public UpdateMeCommandHandler(IUserRepository userRepository, IUserProfileRepository profileRepository)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
    }

    public async Task<UpdateMeResult> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new UpdateMeResult(false, "کاربر یافت نشد", null);

        user.UpdateEmail(request.Email);
        await _userRepository.UpdateAsync(user, cancellationToken);

        var profile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
        {
            var newProfile = Domain.Entities.UserProfile.Create(
                request.UserId,
                request.FirstName,
                request.LastName);
            await _profileRepository.AddAsync(newProfile, cancellationToken);
            profile = newProfile;
        }
        else
        {
            profile.Update(request.FirstName, request.LastName, profile.NationalCode, profile.Gender);
            await _profileRepository.UpdateAsync(profile, cancellationToken);
        }

        var profileDto = new UserProfileDto(
            profile.Id,
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.NationalCode,
            profile.Gender);

        var meDto = new MeDetailDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles(),
            profileDto);

        return new UpdateMeResult(true, null, meDto);
    }
}
