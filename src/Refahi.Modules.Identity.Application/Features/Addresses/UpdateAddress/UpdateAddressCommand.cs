using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Addresses.UpdateAddress;

public sealed record UpdateAddressCommand(
    Guid AddressId,
    Guid UserId,
    string Title,
    int ProvinceId,
    int CityId,
    string FullAddress,
    string PostalCode,
    string ReceiverName,
    string ReceiverPhone,
    string? Plate,
    string? Unit,
    double? Latitude,
    double? Longitude) : IRequest<UserAddressDto>;
