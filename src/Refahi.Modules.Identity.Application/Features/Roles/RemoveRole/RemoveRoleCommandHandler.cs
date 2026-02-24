using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Roles.RemoveRole;

public class RemoveRoleCommandHandler : IRequestHandler<RemoveRoleCommand, RemoveRoleResult>
{
    private readonly IUserRepository _userRepository;

    public RemoveRoleCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<RemoveRoleResult> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return new RemoveRoleResult(false, "Role is required");
        }

        // Find user
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
        {
            return new RemoveRoleResult(false, "User not found");
        }

        // Remove role
        try
        {
            user.RemoveRole(request.Role);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
        catch (DomainException ex)
        {
            return new RemoveRoleResult(false, ex.Message);
        }

        return new RemoveRoleResult(true);
    }
}
