using System;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.ProvisionalBooking;

public sealed class ProvisionalBookingResponse
{
    public Guid BookingId { get; set; }
    public long CustomerPrice { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
