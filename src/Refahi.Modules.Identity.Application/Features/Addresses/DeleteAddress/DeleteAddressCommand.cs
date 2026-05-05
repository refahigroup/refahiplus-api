using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Addresses.DeleteAddress;

public sealed record DeleteAddressCommand(Guid AddressId, Guid UserId) : IRequest<Unit>;
