using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Addresses.AddAddress;

/// <summary>
/// افزودن آدرس جدید برای کاربر.
/// اگر <see cref="IsDefault"/> true باشد، آدرس‌های قبلی پیش‌فرض از حالت پیش‌فرض خارج می‌شوند.
/// </summary>
public sealed record AddAddressCommand(
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
    bool IsDefault) : IRequest<UserAddressDto>;
