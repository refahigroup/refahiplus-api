using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Application.Features.Addresses.Mapping;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.GetUserAddressById;

public class GetUserAddressByIdQueryHandler : IRequestHandler<GetUserAddressByIdQuery, UserAddressDto?>
{
    private readonly IUserAddressRepository _repo;

    public GetUserAddressByIdQueryHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<UserAddressDto?> Handle(GetUserAddressByIdQuery request, CancellationToken cancellationToken)
    {
        var a = await _repo.GetByIdForUserAsync(request.AddressId, request.UserId, cancellationToken);
        return a?.ToDto();
    }
}
