using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Admin.EditUser;

public class AdminEditUserCommandHandler : IRequestHandler<AdminEditUserCommand, AdminEditUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserProfileRepository _profileRepository;

    public AdminEditUserCommandHandler(IUserRepository userRepository, IUserProfileRepository profileRepository)
    {
        _userRepository = userRepository;
        _profileRepository = profileRepository;
    }

    public async Task<AdminEditUserResult> Handle(AdminEditUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new AdminEditUserResult(false, "کاربر یافت نشد");

        try
        {
            var profile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (profile is null)
            {
                var newProfile = UserProfile.Create(request.UserId, request.FirstName, request.LastName, request.NationalCode);
                await _profileRepository.AddAsync(newProfile, cancellationToken);
            }
            else
            {
                profile.Update(request.FirstName, request.LastName, request.NationalCode);
                await _profileRepository.UpdateAsync(profile, cancellationToken);
            }
        }
        catch (DomainException ex)
        {
            return new AdminEditUserResult(false, ex.Message);
        }

        return new AdminEditUserResult(true);
    }
}
