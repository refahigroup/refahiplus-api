using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Features.Auth;

public sealed class ResolveAuthenticationIdentityQueryHandler(IUserRepository users)
    : IRequestHandler<ResolveAuthenticationIdentityQuery, AuthenticationIdentityDto?>
{
    public async Task<AuthenticationIdentityDto?> Handle(
        ResolveAuthenticationIdentityQuery request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier?.Trim();
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains('@'))
            return null;

        var user = await users.GetByMobileOrEmailOrUsernameAsync(identifier, cancellationToken);
        return user is null ? null : Map(user);
    }

    private static AuthenticationIdentityDto Map(User user)
    {
        var eligible = user.IsActive && !user.IsLocked();
        if (eligible && user.MobileApproved && !string.IsNullOrWhiteSpace(user.MobileNumber))
            return new(user.Id, true, user.MobileNumber, OtpReceiptType.Sms);
        if (eligible && user.EmailApproved && !string.IsNullOrWhiteSpace(user.Email))
            return new(user.Id, true, user.Email, OtpReceiptType.Email);
        return new(user.Id, false, null, null);
    }
}

public sealed class GetUserAuthenticationEligibilityQueryHandler(IUserRepository users)
    : IRequestHandler<GetUserAuthenticationEligibilityQuery, UserAuthenticationEligibilityDto>
{
    public async Task<UserAuthenticationEligibilityDto> Handle(
        GetUserAuthenticationEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        return new(request.UserId, user is not null && user.IsActive && !user.IsLocked());
    }
}
