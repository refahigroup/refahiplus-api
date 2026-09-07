using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Commerce;

public sealed class GetUserCommerceContactQueryHandler(IUserRepository users)
    : IRequestHandler<GetUserCommerceContactQuery, UserCommerceContactDto?>
{
    public async Task<UserCommerceContactDto?> Handle(GetUserCommerceContactQuery request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(request.UserId, ct);
        if (user is null) return null;
        var name = user.Profile is null ? string.Empty : $"{user.Profile.FirstName} {user.Profile.LastName}".Trim();
        return new(user.Id, name, user.MobileNumber);
    }
}
