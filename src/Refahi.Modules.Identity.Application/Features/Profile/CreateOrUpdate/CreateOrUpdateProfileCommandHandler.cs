using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Profile.CreateOrUpdate;

public class CreateOrUpdateProfileCommandHandler : IRequestHandler<CreateOrUpdateProfileCommand, CreateOrUpdateProfileResult>
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly IUserRepository _userRepository;

    public CreateOrUpdateProfileCommandHandler(
        IUserProfileRepository profileRepository,
        IUserRepository userRepository)
    {
        _profileRepository = profileRepository;
        _userRepository = userRepository;
    }

    public async Task<CreateOrUpdateProfileResult> Handle(CreateOrUpdateProfileCommand request, CancellationToken cancellationToken)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return new CreateOrUpdateProfileResult(false, "First name is required");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return new CreateOrUpdateProfileResult(false, "Last name is required");
        }

        // Verify user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new CreateOrUpdateProfileResult(false, "User not found");
        }

        // Check if profile already exists
        var existingProfile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        UserProfile profile;

        if (existingProfile == null)
        {
            // Create new profile
            try
            {
                profile = UserProfile.Create(
                    userId: request.UserId,
                    firstName: request.FirstName,
                    lastName: request.LastName,
                    nationalCode: request.NationalCode,
                    gender: request.Gender);

                await _profileRepository.AddAsync(profile, cancellationToken);
            }
            catch (DomainException ex)
            {
                return new CreateOrUpdateProfileResult(false, ex.Message);
            }
        }
        else
        {
            // Update existing profile
            try
            {
                existingProfile.Update(
                    firstName: request.FirstName,
                    lastName: request.LastName,
                    nationalCode: request.NationalCode,
                    gender: request.Gender);

                await _profileRepository.UpdateAsync(existingProfile, cancellationToken);
                profile = existingProfile;
            }
            catch (DomainException ex)
            {
                return new CreateOrUpdateProfileResult(false, ex.Message);
            }
        }

        // Map to DTO
        var profileDto = new UserProfileDto(
            profile.Id,
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.NationalCode,
            profile.Gender);

        return new CreateOrUpdateProfileResult(true, null, profileDto);
    }
}
