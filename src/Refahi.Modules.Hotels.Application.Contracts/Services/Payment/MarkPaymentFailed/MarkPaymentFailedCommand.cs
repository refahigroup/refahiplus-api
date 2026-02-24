using System;
using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Payment.MarkPaymentFailed;

public sealed record MarkPaymentFailedCommand(Guid BookingId) : IRequest<Unit>;
