using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;
using DomainRoles = Refahi.Modules.Identity.Domain.ValueObjects.Roles;

namespace Refahi.Modules.Identity.Application.Features.Roles.AssignRole;

public class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, AssignRoleResult>
{
    private readonly IUserRepository _userRepository;

    public AssignRoleCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AssignRoleResult> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        // Note: Basic validation (Role format, UserId validation) is handled by FluentValidation pipeline
        
        // Find user
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
        {
            return new AssignRoleResult(false, "User not found");
        }

        // Assign role
        try
        {
            user.AssignRole(request.Role, request.AssignedBy);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
        catch (DomainException ex)
        {
            return new AssignRoleResult(false, ex.Message);
        }

        return new AssignRoleResult(true);
    }
}
