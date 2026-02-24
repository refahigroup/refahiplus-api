using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Features.Auth.Login;

public record LoginCommand(string MobileOrEmail, string Password) : IRequest<UserDto?>;

