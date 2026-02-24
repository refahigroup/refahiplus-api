using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.SetPassword;

public class SetPasswordCommandHandler : IRequestHandler<SetPasswordCommand, SetPasswordResult>
{
    private readonly IUserRepository _userRepository;

    public SetPasswordCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<SetPasswordResult> Handle(SetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.MobileOrEmail))
        {
            return new SetPasswordResult(false, "Mobile number or email is required");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new SetPasswordResult(false, "Password is required");
        }

        // Find user
        var user = await _userRepository.GetByMobileOrEmailAsync(request.MobileOrEmail, cancellationToken);

        if (user == null)
        {
            return new SetPasswordResult(false, "User not found");
        }

        // Set password
        try
        {
            user.SetPassword(request.Password);
        }
        catch (WeakPasswordException ex)
        {
            return new SetPasswordResult(false, ex.Message);
        }
        catch (DomainException ex)
        {
            return new SetPasswordResult(false, ex.Message);
        }

        // Update user
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new SetPasswordResult(true);
    }
}
