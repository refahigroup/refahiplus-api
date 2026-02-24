using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.Payment.MarkPaymentFailed;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Application.Payment.MarkFailed;

public sealed class MarkPaymentFailedCommandHandler: IRequestHandler<MarkPaymentFailedCommand, Unit>
{
    private readonly IBookingRepository _repository;

    public MarkPaymentFailedCommandHandler(IBookingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(
        MarkPaymentFailedCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _repository.GetAsync(new BookingId(request.BookingId));
        if (booking == null)
            throw new Exception("Booking not found");

        booking.MarkPaymentFailed(DateTime.UtcNow);
        await _repository.SaveChangesAsync();

        return Unit.Value;
    }
}
