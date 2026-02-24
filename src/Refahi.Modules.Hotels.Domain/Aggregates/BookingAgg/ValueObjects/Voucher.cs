namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;

public sealed class Voucher
{
    public string? VoucherNumber { get; }
    public string? Url { get; }

    public Voucher(string? voucherNumber, string? url)
    {
        VoucherNumber = string.IsNullOrWhiteSpace(voucherNumber) ? null : voucherNumber.Trim();
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }
}
