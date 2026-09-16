using System;
using MediatR;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.Recovery;

public sealed record RecoverFundingSessionCommand(Guid SessionId) : IRequest;

public sealed record RetryFundingReversalCommand(Guid SessionId) : IRequest;
