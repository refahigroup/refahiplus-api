using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Contact;

public sealed class GetUserContactQueryHandler(IUserRepository users)
    : IRequestHandler<GetUserContactQuery, UserContactDto?>
{
    public async Task<UserContactDto?> Handle(GetUserContactQuery request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(request.UserId, ct);
        if (user is null) return null;
        var name = user.Profile is null ? string.Empty : $"{user.Profile.FirstName} {user.Profile.LastName}".Trim();
        return new(user.Id, name, user.MobileNumber);
    }
}
