using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(Guid AddressId, Guid UserId)
    : IRequest<UserAddressDto>;
