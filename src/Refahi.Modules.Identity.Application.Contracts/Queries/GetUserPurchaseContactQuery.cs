using MediatR;
using System;

namespace Refahi.Modules.Identity.Application.Contracts.Queries;

public sealed record GetUserPurchaseContactQuery(Guid UserId) : IRequest<UserPurchaseContactDto?>;
public sealed record UserPurchaseContactDto(Guid UserId, string FullName, string? MobileNumber);
