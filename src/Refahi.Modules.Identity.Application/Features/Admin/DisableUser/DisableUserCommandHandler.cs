using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Admin.DisableUser;

public class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, DisableUserResult>
{
    private readonly IUserRepository _userRepository;

    public DisableUserCommandHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<DisableUserResult> Handle(DisableUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new DisableUserResult(false, "کاربر یافت نشد");

        user.Deactivate();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new DisableUserResult(true);
    }
}
