using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Orders;

public sealed class GetOrderUserSummariesQueryHandler
    : IRequestHandler<GetOrderUserSummariesQuery, IReadOnlyList<OrderUserSummaryDto>>
{
    private readonly IUserRepository _userRepository;

    public GetOrderUserSummariesQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<IReadOnlyList<OrderUserSummaryDto>> Handle(
        GetOrderUserSummariesQuery request,
        CancellationToken cancellationToken)
    {
        List<User> users;

        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            if (!MobileNumberSearchNormalizer.TryNormalize(request.MobileNumber, out var normalizedMobileNumber)
                || string.IsNullOrEmpty(normalizedMobileNumber))
            {
                return [];
            }

            users = await _userRepository.SearchByMobileNumberAsync(
                normalizedMobileNumber,
                cancellationToken);

            if (request.UserIds is { Count: > 0 })
            {
                var requestedUserIds = request.UserIds.ToHashSet();
                users = users.Where(user => requestedUserIds.Contains(user.Id)).ToList();
            }
        }
        else if (request.UserIds is { Count: > 0 })
        {
            users = await _userRepository.GetByIdsAsync(request.UserIds, cancellationToken);
        }
        else
        {
            return [];
        }

        return users.Select(user => new OrderUserSummaryDto(
            user.Id,
            user.Profile?.FirstName,
            user.Profile?.LastName,
            user.MobileNumber)).ToList();
    }
}
