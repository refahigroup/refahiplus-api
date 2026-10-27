using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Application.Features.Operations;

public sealed record GetUncertainEntertainmentReservationsQuery : IRequest<IReadOnlyList<UncertainEntertainmentReservation>>;
public sealed record UncertainEntertainmentReservation(Guid Id, Guid UserId, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PayableUntil);
public sealed class GetUncertainEntertainmentReservationsHandler(IEntertainmentSessionRepository repository) : IRequestHandler<GetUncertainEntertainmentReservationsQuery, IReadOnlyList<UncertainEntertainmentReservation>>
{
    public async Task<IReadOnlyList<UncertainEntertainmentReservation>> Handle(GetUncertainEntertainmentReservationsQuery request, CancellationToken ct) =>
        (await repository.GetUncertainAsync(ct)).Select(x => new UncertainEntertainmentReservation(x.Id, x.UserId, x.Status.ToString(), x.CreatedAt, x.PayableUntil)).ToArray();
}
public sealed record ResolveEntertainmentReservationCommand(Guid Id, Guid ActorId, string Outcome, string Evidence, string IdempotencyKey) : IRequest;
public sealed class ResolveEntertainmentReservationValidator : AbstractValidator<ResolveEntertainmentReservationCommand>
{
    public ResolveEntertainmentReservationValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("شناسه رزرو معتبر نیست");
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("هویت مدیر معتبر نیست");
        RuleFor(x => x.Outcome).Must(x => x is "notcreated" or "expired" or "released").WithMessage("نتیجه بررسی معتبر نیست");
        RuleFor(x => x.Evidence).NotEmpty().MaximumLength(4000).WithMessage("ثبت مستند قطعی Provider الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160).WithMessage("کلید یکتایی معتبر نیست");
    }
}
public sealed class ResolveEntertainmentReservationHandler(IEntertainmentSessionRepository repository, IEntertainmentMutationLock gate, IEntertainmentSecretProtector secrets) : IRequestHandler<ResolveEntertainmentReservationCommand>
{
    public async Task<Unit> Handle(ResolveEntertainmentReservationCommand request, CancellationToken ct)
    {
        var session = await repository.GetAsync(request.Id, ct) ?? throw new EntertainmentDomainException("رزرو یافت نشد", "RESERVATION_NOT_FOUND");
        await using var userLock = await gate.AcquireAsync(session.UserId, ct);
        await using var sessionLock = await gate.AcquireAsync(session.Id, ct);
        session = await repository.GetFreshAsync(session.Id, ct) ?? throw new EntertainmentDomainException("رزرو یافت نشد", "RESERVATION_NOT_FOUND");
        var fingerprint = secrets.Hash(System.Text.Json.JsonSerializer.Serialize(new { request.ActorId, request.Outcome, request.Evidence }));
        if (session.ResolutionKey == request.IdempotencyKey)
        {
            if (session.ResolutionFingerprint != fingerprint) throw new EntertainmentDomainException("کلید یکتایی با مستند دیگری استفاده شده است", "IDEMPOTENCY_CONFLICT");
            return Unit.Value;
        }
        session.ResolveUnpaid(request.ActorId, request.IdempotencyKey, fingerprint, secrets.Protect(request.Evidence), request.Outcome);
        await repository.SaveAsync(ct);
        return Unit.Value;
    }
}
