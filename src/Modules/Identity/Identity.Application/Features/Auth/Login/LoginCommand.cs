using Identity.Application.Contracts.Models;
using MediatR;

namespace Identity.Application.Features.Auth.Login;

public record LoginCommand(string Username, string Password) : IRequest<UserDto>;
