using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class PreviewChargeRequestValidator : AbstractValidator<PreviewChargeRequestCommand>
{
    public PreviewChargeRequestValidator()
    {
        RuleFor(x => x.Operator)
            .IsInEnum()
            .WithMessage("اپراتور معتبر نیست");

        RuleFor(x => x.ServiceType)
            .IsInEnum()
            .WithMessage("نوع خدمت معتبر نیست");

        RuleFor(x => x.DestinationMobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");

        RuleFor(x => x.PinCount)
            .InclusiveBetween(1, 100)
            .When(x => x.ServiceType == ChargeServiceType.PinCharge)
            .WithMessage("تعداد پین باید بین یک تا صد باشد");
    }
}

public sealed class PreviewChargeRequestHandler(ChargeRequestQuoteService quotes): IRequestHandler<PreviewChargeRequestCommand, ChargeRequestQuoteResponse>
{
    public async Task<ChargeRequestQuoteResponse> Handle(PreviewChargeRequestCommand command, CancellationToken ct)
    {
        var quote = await quotes.ResolveAsync(new ChargeSelection(
            command.Operator,
            command.ServiceType,
            command.DestinationMobileNumber,
            command.ProviderProductId,
            command.RequestedAmountMinor,
            command.PinCategoryId,
            command.PinCount), ct);

        return new ChargeRequestQuoteResponse(
            quote.ExpireAt,
            quote.ProviderCostMinor,
            quote.MarkupAmountMinor,
            quote.FinalAmountMinor,
            "IRR");
    }
}
