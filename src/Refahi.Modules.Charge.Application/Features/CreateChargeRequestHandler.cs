using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequestCommand>
{
    public CreateChargeRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.Operator)
            .IsInEnum()
            .WithMessage("اپراتور معتبر نیست");

        RuleFor(x => x.ServiceType)
            .IsInEnum()
            .WithMessage("نوع خدمت معتبر نیست");

        RuleFor(x => x.DestinationMobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");

        RuleFor(x => x.ExpectedFinalAmountMinor)
            .GreaterThan(0)
            .WithMessage("مبلغ مورد انتظار معتبر نیست");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().MaximumLength(200)
            .WithMessage("کلید تکرارپذیری الزامی است");

        RuleFor(x => x.PinCount).InclusiveBetween(1, 100)
            .When(x => x.ServiceType == ChargeServiceType.PinCharge)
            .WithMessage("تعداد پین باید بین یک تا صد باشد");
    }
}

public sealed class CreateChargeRequestHandler : IRequestHandler<CreateChargeRequestCommand, CreateChargeRequestResponse>
{
    private readonly IChargeRequestRepository _requests;
    private readonly ChargeRequestQuoteService _quotes;

    public CreateChargeRequestHandler(
        IChargeRequestRepository requests,
        ChargeRequestQuoteService quotes)
    {
        _requests = requests;
        _quotes = quotes;
    }

    public async Task<CreateChargeRequestResponse> Handle(CreateChargeRequestCommand command, CancellationToken ct)
    {
        var existing = await _requests.GetByIdempotencyKeyAsync(command.UserId, command.IdempotencyKey.Trim(), ct);

        if (existing is not null)
            return new(
                existing.Id,
                existing.Status.ToString(),
                existing.ExpireAt,
                existing.ProviderCostMinor,
                existing.MarkupAmountMinor,
                existing.FinalAmountMinor,
                existing.Currency
            );

        var quote = await _quotes.ResolveAsync(new ChargeSelection(
            command.Operator,
            command.ServiceType,
            command.DestinationMobileNumber,
            command.ProviderProductId,
            command.RequestedAmountMinor,
            command.PinCategoryId,
            command.PinCount), ct
        );

        if (quote.FinalAmountMinor != command.ExpectedFinalAmountMinor)
        {
            throw new ChargeQuoteChangedException(new ChargeRequestQuoteResponse(
                quote.ExpireAt,
                quote.ProviderCostMinor,
                quote.MarkupAmountMinor,
                quote.FinalAmountMinor,
                "IRR"));
        }

        var now = DateTime.UtcNow;

        var request = ChargeRequest.Create(
            command.UserId,
            quote.ProviderName,
            command.Operator,
            command.ServiceType,
            command.DestinationMobileNumber,
            command.OriginMobileNumber,
            quote.ProductId,
            quote.Caption,
            quote.ProductCategory,
            quote.PayBill,
            command.PinCategoryId,
            command.ServiceType == ChargeServiceType.PinCharge ? command.PinCount : 1,
            quote.ProductSnapshotJson,
            quote.ProviderCostMinor,
            quote.MarkupRuleId,
            quote.MarkupPercent,
            quote.MarkupFixedMinor,
            quote.MarkupAmountMinor,
            quote.FinalAmountMinor,
            command.IdempotencyKey,
            now,
            quote.ExpireAt
        );

        await _requests.AddAsync(request, ct);
        await _requests.SaveChangesAsync(ct);

        return new(
            request.Id,
            request.Status.ToString(),
            request.ExpireAt,
            request.ProviderCostMinor,
            request.MarkupAmountMinor,
            request.FinalAmountMinor,
            request.Currency
        );
    }
}
