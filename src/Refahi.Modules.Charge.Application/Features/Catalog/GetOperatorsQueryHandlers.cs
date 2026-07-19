using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Features.Catalog;

public sealed class GetOperatorsQueryHandlers : IRequestHandler<GetOperatorsQuery, IReadOnlyList<ChargeOperatorDto>>
{
    public Task<IReadOnlyList<ChargeOperatorDto>> Handle(GetOperatorsQuery q, CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<ChargeOperatorDto>>(
            [
                new(1, "irancell", "ایرانسل", ChargeCapabilityPolicy.For(ChargeOperator.Irancell)),
                new(2, "mci", "همراه اول", ChargeCapabilityPolicy.For(ChargeOperator.Mci)),
                new(3, "rightel", "رایتل", ChargeCapabilityPolicy.For(ChargeOperator.Rightel)),
                new(4, "shatel", "شاتل", ChargeCapabilityPolicy.For(ChargeOperator.Shatel)),
                new(5, "taliya", "تالیا", ChargeCapabilityPolicy.For(ChargeOperator.Taliya))
            ]
        );
    }
}
