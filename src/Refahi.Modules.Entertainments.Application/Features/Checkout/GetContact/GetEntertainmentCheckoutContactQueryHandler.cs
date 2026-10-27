using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.GetContact;

public sealed class GetEntertainmentCheckoutContactQueryHandler(IMediator mediator) : IRequestHandler<GetEntertainmentCheckoutContactQuery, EntertainmentCheckoutContactDto>
{
    public async Task<EntertainmentCheckoutContactDto> Handle(GetEntertainmentCheckoutContactQuery request, CancellationToken ct)
    {
        var value = await mediator.Send(new GetUserContactQuery(request.UserId), ct);

        return new(
            value?.FullName ?? string.Empty,
            value?.MobileNumber
        );
    }
}
