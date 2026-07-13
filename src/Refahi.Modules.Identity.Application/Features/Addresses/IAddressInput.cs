namespace Refahi.Modules.Identity.Application.Features.Addresses;

public interface IAddressInput
{
    string Title { get; }
    int ProvinceId { get; }
    int CityId { get; }
    string FullAddress { get; }
    string PostalCode { get; }
    string ReceiverName { get; }
    string ReceiverPhone { get; }
    string? Plate { get; }
    string? Unit { get; }
}
