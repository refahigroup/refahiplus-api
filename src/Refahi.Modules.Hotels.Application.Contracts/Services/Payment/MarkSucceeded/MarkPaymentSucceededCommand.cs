using System;
using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Payment.MarkSucceeded;

public sealed record MarkPaymentSucceededCommand(Guid BookingId) : IRequest<Unit>;
