using System;

namespace Refahi.Modules.Identity.Application.Contracts.Models;

/// <summary>
/// DTO آدرس کاربر — برای پاسخ Endpoint‌ها و Inter-module Queries.
/// </summary>
public sealed record UserAddressDto(
    Guid Id,
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
    double? Longitude,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
