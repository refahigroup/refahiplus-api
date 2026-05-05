using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Features.Addresses.Mapping;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.GetMyAddresses;

public class GetMyAddressesQueryHandler : IRequestHandler<GetMyAddressesQuery, IReadOnlyList<UserAddressDto>>
{
    private readonly IUserAddressRepository _repo;

    public GetMyAddressesQueryHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<UserAddressDto>> Handle(GetMyAddressesQuery request, CancellationToken cancellationToken)
    {
        var addresses = await _repo.GetByUserIdAsync(request.UserId, cancellationToken);
        return addresses.Select(a => a.ToDto()).ToList();
    }
}
