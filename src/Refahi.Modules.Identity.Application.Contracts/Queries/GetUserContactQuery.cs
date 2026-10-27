using System;
using MediatR;
namespace Refahi.Modules.Identity.Application.Contracts.Queries;
public sealed record GetUserContactQuery(Guid UserId) : IRequest<UserContactDto?>;
public sealed record UserContactDto(Guid UserId, string FullName, string? MobileNumber);
