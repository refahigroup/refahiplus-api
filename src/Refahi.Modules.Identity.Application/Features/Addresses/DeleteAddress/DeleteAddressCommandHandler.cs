using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Addresses.DeleteAddress;

public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand, Unit>
{
    private readonly IUserAddressRepository _repo;

    public DeleteAddressCommandHandler(IUserAddressRepository repo) => _repo = repo;

    public async Task<Unit> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _repo.GetByIdForUserAsync(request.AddressId, request.UserId, cancellationToken)
            ?? throw new DomainException("آدرس یافت نشد", "ADDRESS_NOT_FOUND");

        var wasDefault = address.IsDefault;
        await _repo.DeleteAsync(address, cancellationToken);

        // اگر آدرس پیش‌فرض حذف شد، اولین آدرس باقی‌مانده را پیش‌فرض کنیم
        if (wasDefault)
        {
            var remaining = await _repo.GetByUserIdAsync(request.UserId, cancellationToken);
            var first = remaining.FirstOrDefault();
            if (first is not null)
            {
                first.MarkAsDefault();
                await _repo.UpdateAsync(first, cancellationToken);
            }
        }

        return Unit.Value;
    }
}
