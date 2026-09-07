using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;

namespace Refahi.Modules.Commerce.Application.Features.Checkout.GetContact;

public sealed class GetCommerceCheckoutContactQueryHandler(IMediator mediator) : IRequestHandler<GetCommerceCheckoutContactQuery, CommerceCheckoutContactDto>
{
    public async Task<CommerceCheckoutContactDto> Handle(GetCommerceCheckoutContactQuery request, CancellationToken ct)
    {
        var value = await mediator.Send(new GetUserCommerceContactQuery(request.UserId), ct);

        return new(
            value?.FullName ?? string.Empty,
            value?.MobileNumber
        );
    }
}
