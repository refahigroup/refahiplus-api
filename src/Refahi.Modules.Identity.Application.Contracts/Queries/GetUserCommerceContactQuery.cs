using MediatR;
using System;

namespace Refahi.Modules.Identity.Application.Contracts.Queries;

public sealed record GetUserCommerceContactQuery(Guid UserId) : IRequest<UserCommerceContactDto?>;
public sealed record UserCommerceContactDto(Guid UserId, string FullName, string? MobileNumber);
