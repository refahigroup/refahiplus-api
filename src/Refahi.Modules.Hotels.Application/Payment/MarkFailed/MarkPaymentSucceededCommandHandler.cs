using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Services.Payment.MarkSucceeded;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

namespace Refahi.Modules.Hotels.Application.Payment.MarkFailed;

public sealed class MarkPaymentSucceededCommandHandler: IRequestHandler<MarkPaymentSucceededCommand, Unit>
{
    private readonly IBookingRepository _repository;
    private readonly IHotelProvider _provider;

    public MarkPaymentSucceededCommandHandler(
        IBookingRepository repository,
        IHotelProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }

    public async Task<Unit> Handle(
        MarkPaymentSucceededCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _repository.GetAsync(new BookingId(request.BookingId));
        if (booking == null)
            throw new Exception("Booking not found");

        // update domain state
        booking.MarkPaymentSucceeded(DateTime.UtcNow);

        // 1. confirm on provider
        await _provider.ConfirmBookingAsync(booking.ProviderBookingCode.Value);

        // 2. fetch provider final status
        var status = await _provider.GetBookingStatusAsync(booking.ProviderBookingCode.Value);

        if (status.Status.Equals("confirmed", StringComparison.OrdinalIgnoreCase))
        {
            booking.Confirm(
                new Voucher(status.VoucherNumber, status.VoucherUrl),
                DateTime.UtcNow
            );
        }
        else
        {
            booking.ConfirmFailed(status.ProviderMessage ?? "Provider failed", DateTime.UtcNow);
        }

        await _repository.SaveChangesAsync();

        return Unit.Value;
    }
}
