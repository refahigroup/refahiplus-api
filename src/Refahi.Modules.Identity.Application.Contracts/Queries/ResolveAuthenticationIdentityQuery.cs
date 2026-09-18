using System;
using MediatR;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Contracts.Queries;

public sealed record ResolveAuthenticationIdentityQuery(string Identifier)
    : IRequest<AuthenticationIdentityDto?>;

public sealed record GetUserAuthenticationEligibilityQuery(Guid UserId)
    : IRequest<UserAuthenticationEligibilityDto>;

public sealed record AuthenticationIdentityDto(
    Guid UserId,
    bool CanAuthenticate,
    string? OtpReceipt,
    OtpReceiptType? OtpReceiptType);

public sealed record UserAuthenticationEligibilityDto(Guid UserId, bool CanAuthenticate);
