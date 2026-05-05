using System;
using System.Collections.Generic;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Addresses.GetMyAddresses;

/// <summary>
/// لیست آدرس‌های کاربر جاری.
/// </summary>
public sealed record GetMyAddressesQuery(Guid UserId)
    : IRequest<IReadOnlyList<UserAddressDto>>;
