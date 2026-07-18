using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using System.Text.Json;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class ConvertChargeRequestToOrderValidator : AbstractValidator<ConvertChargeRequestToOrderCommand>
{
    public ConvertChargeRequestToOrderValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("شناسه درخواست شارژ الزامی است");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("شناسه کاربر الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید تکرارپذیری الزامی است");
    }
}

public sealed class ConvertChargeRequestToOrderHandler : IRequestHandler<ConvertChargeRequestToOrderCommand, ConvertChargeRequestToOrderResponse>
{
    private readonly IChargeRequestRepository _requests;
    private readonly IMediator _mediator;

    public ConvertChargeRequestToOrderHandler(IChargeRequestRepository requests, IMediator mediator)
    {
        _requests = requests;
        _mediator = mediator;
    }

    public async Task<ConvertChargeRequestToOrderResponse> Handle(ConvertChargeRequestToOrderCommand command, CancellationToken ct)
    {
        var request = await _requests.GetForUserAsync(command.RequestId, command.UserId, ct)
            ?? throw new ArgumentException("درخواست شارژ یافت نشد");

        if (request.OrderId.HasValue)
            return new(request.Id, request.OrderId.Value, string.Empty, request.FinalAmountMinor, request.Status.ToString());

        if (request.Status != ChargeRequestStatus.Created)
            throw new ArgumentException("درخواست شارژ در وضعیت قابل تبدیل به سفارش نیست");

        if (request.ExpireAt <= DateTime.UtcNow)
        {
            request.MarkExpired(DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct);

            throw new ArgumentException("مهلت درخواست شارژ به پایان رسیده است");
        }

        var order = await _mediator.Send(
            new CreateOrderCommand(
                request.UserId,
                "Charge",
                request.Id,
                [
                    new CreateOrderItemInput(
                        BuildTitle(
                            request.ServiceType,
                            request.Operator,
                            request.DestinationMobileNumber),
                            request.FinalAmountMinor,
                            1,
                            0,
                            request.Id,
                            CategoryCode(request.ServiceType
                        ),
                        ["charge", request.ServiceType.ToString().ToLowerInvariant()],
                        JsonSerializer.Serialize(new
                        {
                            reference_type = "ChargeRequest",
                            request_id = request.Id,
                            provider = request.ProviderName,
                            @operator = request.Operator.ToString(),
                            service_type = request.ServiceType.ToString(),
                            destination = Mask(request.DestinationMobileNumber),
                            provider_product_id = request.ProviderProductId,
                            provider_cost_minor = request.ProviderCostMinor,
                            markup_amount_minor = request.MarkupAmountMinor
                        }),
                        0
                    )
                ],
                $"charge-request-order-{command.IdempotencyKey.Trim()}",
                "ChargeRequest",
                SagaId: request.SagaId,
                PayableUntil: new DateTimeOffset(request.ExpireAt)
            ),
            ct
        );

        request.ConvertToOrder(order.OrderId, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);

        return new(request.Id, order.OrderId, order.OrderNumber, order.FinalAmountMinor, request.Status.ToString());
    }

    private static string BuildTitle(ChargeServiceType service, ChargeOperator op, string mobile)
        => $"{ServiceCaption(service)} {OperatorCaption(op)} برای {Mask(mobile)}";

    private static string ServiceCaption(ChargeServiceType type) => type switch
    {
        ChargeServiceType.DirectCharge => "خرید شارژ",
        ChargeServiceType.InternetPackage => "خرید بسته اینترنت",
        ChargeServiceType.PostpaidBill => "پرداخت قبض",
        ChargeServiceType.CreditLimit => "افزایش حد اعتبار",
        _ => "خرید پین شارژ"
    };

    private static string OperatorCaption(ChargeOperator op) => op switch
    {
        ChargeOperator.Irancell => "ایرانسل",
        ChargeOperator.Mci => "همراه اول",
        ChargeOperator.Rightel => "رایتل",
        ChargeOperator.Shatel => "شاتل",
        _ => "تالیا"
    };

    private static string CategoryCode(ChargeServiceType type) => $"mobile-charge.{type switch
    {
        ChargeServiceType.DirectCharge => "direct",
        ChargeServiceType.InternetPackage => "internet",
        ChargeServiceType.PostpaidBill => "bill",
        ChargeServiceType.CreditLimit => "credit",
        _ => "pin"
    }}";

    internal static string Mask(string value) =>
        value.Length < 7 ? "***" : $"{value[..4]}***{value[^4..]}";
}
