using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetAllOrders;

public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, PaginatedOrdersResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISender _sender;

    public GetAllOrdersQueryHandler(IOrderRepository orderRepository, ISender sender)
    {
        _orderRepository = orderRepository;
        _sender = sender;
    }

    public async Task<PaginatedOrdersResponse> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrderUserSummaryDto> matchedUsers = [];
        IReadOnlyCollection<Guid>? allowedUserIds = null;

        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            if (!MobileNumberSearchNormalizer.TryNormalize(request.MobileNumber, out var normalizedMobileNumber))
                throw new ArgumentException("شماره موبایل فقط می‌تواند شامل رقم، فاصله یا خط تیره باشد");

            if (normalizedMobileNumber is null || normalizedMobileNumber.Length < 4)
                throw new ArgumentException("برای جستجوی شماره موبایل حداقل ۴ رقم وارد کنید");

            matchedUsers = await _sender.Send(
                new GetOrderUserSummariesQuery(MobileNumber: normalizedMobileNumber),
                cancellationToken);
            allowedUserIds = matchedUsers.Select(user => user.UserId).ToArray();
        }

        var orders = await _orderRepository.GetAllAsync(
            request.PageNumber, request.PageSize,
            request.Status, request.UserId, request.SourceModule,
            allowedUserIds,
            cancellationToken);

        var total = await _orderRepository.CountAllAsync(
            request.Status, request.UserId, request.SourceModule,
            allowedUserIds,
            cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        IReadOnlyList<OrderUserSummaryDto> pageUsers = matchedUsers;
        if (string.IsNullOrWhiteSpace(request.MobileNumber) && orders.Count > 0)
        {
            pageUsers = await _sender.Send(
                new GetOrderUserSummariesQuery(
                    UserIds: orders.Select(order => order.UserId).Distinct().ToArray()),
                cancellationToken);
        }

        var usersById = pageUsers.ToDictionary(user => user.UserId);
        var summaries = orders.Select(o =>
        {
            usersById.TryGetValue(o.UserId, out var user);
            return new OrderSummaryDto(
            Id: o.Id,
            OrderNumber: o.OrderNumber,
            FinalAmountMinor: o.FinalAmountMinor,
            Status: o.Status.ToString(),
            SourceModule: o.SourceModule,
            ItemCount: o.Items.Count,
            CreatedAt: o.CreatedAt,
            FirstName: user?.FirstName,
            LastName: user?.LastName,
            MobileNumber: user?.MobileNumber);
        }).ToList();

        return new PaginatedOrdersResponse(summaries, request.PageNumber, request.PageSize, total, totalPages);
    }
}
