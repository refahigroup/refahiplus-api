using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ValidateHotelRequestForOrder;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;

namespace Refahi.Modules.Hotels.Application.HotelRequests.ValidateHotelRequestForOrder;

public sealed class ValidateHotelRequestForOrderCommandHandler
    : IRequestHandler<ValidateHotelRequestForOrderCommand, ValidateHotelRequestForOrderResponse>
{
    private readonly IHotelRequestRepository _repository;

    public ValidateHotelRequestForOrderCommandHandler(IHotelRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidateHotelRequestForOrderResponse> Handle(
        ValidateHotelRequestForOrderCommand request,
        CancellationToken cancellationToken)
    {
        var hotelRequest = await _repository.GetAsync(request.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست هتل یافت نشد");

        if (hotelRequest.UserId != request.UserId)
            throw new UnauthorizedAccessException("دسترسی به این درخواست هتل مجاز نیست");

        var now = DateTime.UtcNow;
        if (hotelRequest.Status == HotelRequestStatus.Created && hotelRequest.ExpireAt <= now)
        {
            hotelRequest.MarkExpired(now);
            await _repository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("مهلت درخواست هتل به پایان رسیده است");
        }

        if (hotelRequest.Status == HotelRequestStatus.Expired)
            throw new InvalidOperationException("مهلت درخواست هتل به پایان رسیده است");

        if (hotelRequest.Status == HotelRequestStatus.Cancelled)
            throw new InvalidOperationException("درخواست هتل لغو شده است");

        if (hotelRequest.Status == HotelRequestStatus.ConvertedToOrder)
            throw new InvalidOperationException("برای این درخواست هتل قبلاً سفارش ایجاد شده است");

        return new ValidateHotelRequestForOrderResponse(
            hotelRequest.Id,
            hotelRequest.UserId,
            hotelRequest.TotalPrice,
            hotelRequest.Currency,
            hotelRequest.Status.ToString(),
            hotelRequest.ExpireAt);
    }
}
