using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Application.Services;

internal static class StoreVariantCapacityService
{
    public static DateOnly? NormalizeAndValidateUsageDate(ProductVariant variant, DateOnly? usageDate)
    {
        var normalizedUsageDate = usageDate;

        if (variant.FromDate.HasValue &&
            variant.ToDate.HasValue &&
            variant.FromDate.Value == variant.ToDate.Value &&
            normalizedUsageDate is null)
        {
            normalizedUsageDate = variant.FromDate.Value;
        }

        if (variant.RequiresUsageDate && normalizedUsageDate is null)
            throw new StoreDomainException("تاریخ استفاده برای این خدمت الزامی است.", "USAGE_DATE_REQUIRED");

        if (normalizedUsageDate.HasValue && (!variant.FromDate.HasValue || !variant.ToDate.HasValue))
            throw new StoreDomainException("خرید این خدمت با تنظیمات فعلی امکان‌پذیر نیست.", "INVALID_VARIANT_USAGE_DATE");

        variant.ValidateOrderEligibility(normalizedUsageDate);
        return normalizedUsageDate;
    }

    public static async Task EnsureCapacityAvailableAsync(
        ProductVariant variant,
        DateOnly? usageDate,
        int quantity,
        IMediator mediator,
        Guid? excludeOrderId,
        CancellationToken cancellationToken)
    {
        if (variant.CapacityType == VariantCapacityType.Unlimited)
        {
            variant.EnsureCapacityAvailable(quantity, soldCountInScope: 0);
            return;
        }

        var soldQuantity = await mediator.Send(
            new GetStoreVariantSoldQuantityQuery(
                variant.Id,
                variant.CapacityType == VariantCapacityType.PerEligibleDay ? usageDate : null,
                ToOrdersCapacityScope(variant.CapacityType),
                excludeOrderId),
            cancellationToken);

        try
        {
            variant.EnsureCapacityAvailable(quantity, soldQuantity);
        }
        catch (StoreDomainException ex) when (ex.ErrorCode == "INSUFFICIENT_VARIANT_CAPACITY")
        {
            var message = variant.CapacityType == VariantCapacityType.PerEligibleDay
                ? "ظرفیت این خدمت برای تاریخ انتخاب‌شده تکمیل شده است."
                : "ظرفیت این خدمت تکمیل شده است.";

            throw new StoreDomainException(message, ex.ErrorCode);
        }
    }

    private static StoreVariantCapacityScope ToOrdersCapacityScope(VariantCapacityType capacityType)
        => capacityType switch
        {
            VariantCapacityType.TotalPeriod => StoreVariantCapacityScope.TotalPeriod,
            VariantCapacityType.PerEligibleDay => StoreVariantCapacityScope.PerEligibleDay,
            _ => StoreVariantCapacityScope.TotalPeriod
        };
}
