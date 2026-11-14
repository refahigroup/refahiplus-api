using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Vouchers.Validate;
public sealed class ValidatePoolVoucherHandler(PoolVoucherAccess access) : IRequestHandler<ValidatePoolVoucherQuery, PoolVoucherValidationDto>
{
    public async Task<PoolVoucherValidationDto> Handle(ValidatePoolVoucherQuery r, CancellationToken ct)
    { var found = await access.Find(r.OperatorId, r.PoolId, r.Code, ct); return access.View(found.Purchase, found.Request); }
}
