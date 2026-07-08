using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class GetChargeRequestPinsHandler : IRequestHandler<GetChargeRequestPinsQuery, IReadOnlyList<ChargePinDeliveryDto>?>
{
    private readonly IChargeRequestRepository _requests;
    private readonly IChargeSecretProtector _protector;
    public GetChargeRequestPinsHandler(IChargeRequestRepository requests, IChargeSecretProtector protector)
    { _requests = requests; _protector = protector; }

    public async Task<IReadOnlyList<ChargePinDeliveryDto>?> Handle(GetChargeRequestPinsQuery query, CancellationToken ct)
    {
        var request = await _requests.GetForUserAsync(query.RequestId, query.UserId, ct);
        if (request is null) return null;
        if (request.ServiceType != ChargeServiceType.PinCharge || request.Status != ChargeRequestStatus.Fulfilled)
            throw new ArgumentException("پین‌های این درخواست هنوز قابل دریافت نیستند");
        return request.Pins.Select(x => new ChargePinDeliveryDto(
            _protector.Unprotect(x.EncryptedSerial), _protector.Unprotect(x.EncryptedCode), x.AmountMinor)).ToArray();
    }
}

public sealed class CancelChargeRequestHandler : IRequestHandler<CancelChargeRequestCommand, bool>
{
    private readonly IChargeRequestRepository _requests;
    public CancelChargeRequestHandler(IChargeRequestRepository requests) => _requests = requests;

    public async Task<bool> Handle(CancelChargeRequestCommand command, CancellationToken ct)
    {
        var request = await _requests.GetForUserAsync(command.RequestId, command.UserId, ct);
        if (request is null) return false;
        request.Cancel(DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);
        return true;
    }
}
