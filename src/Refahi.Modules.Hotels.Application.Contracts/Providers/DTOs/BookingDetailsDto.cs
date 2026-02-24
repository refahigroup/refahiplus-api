namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class BookingDetailsDto
    {
        public Guid BookingId { get; set; }
        public string ProviderCode { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public long CustomerPrice { get; set; }

        public IEnumerable<GuestDto> Guests { get; set; } = Enumerable.Empty<GuestDto>();

        public string? VoucherUrl { get; set; }
    }

