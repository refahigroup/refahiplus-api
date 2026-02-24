using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Auth.Me;

public record MeQuery(Guid UserId) : IRequest<UserDto?>;
