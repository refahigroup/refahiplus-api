using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Contracts.Queries;

/// <summary>
/// Inter-module Query — دریافت یک آدرس به‌همراه چک مالکیت کاربر.
/// قابل استفاده توسط ماژول‌های دیگر (مثل Store) برای ساخت Snapshot آدرس روی Order.
/// </summary>
public sealed record GetUserAddressByIdQuery(Guid AddressId, Guid UserId)
    : IRequest<UserAddressDto?>;
