using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Admin.EnableUser;

public class EnableUserCommandHandler : IRequestHandler<EnableUserCommand, EnableUserResult>
{
    private readonly IUserRepository _userRepository;

    public EnableUserCommandHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<EnableUserResult> Handle(EnableUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new EnableUserResult(false, "کاربر یافت نشد");

        user.Activate();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new EnableUserResult(true);
    }
}
