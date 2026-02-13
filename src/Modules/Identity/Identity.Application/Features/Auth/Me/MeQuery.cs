using Identity.Application.Contracts.Models;
using MediatR;

namespace Identity.Application.Features.Auth.Me;

public record MeQuery(string UserId) : IRequest<UserDto>;
