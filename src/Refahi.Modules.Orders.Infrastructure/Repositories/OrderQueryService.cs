using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using System.Text.Json;

namespace Refahi.Modules.Orders.Infrastructure.Repositories;

public class OrderQueryService : IOrderQueryService
{
    private readonly OrdersDbContext _context;

    public OrderQueryService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderSummaryDto>> GetUserOrderSummariesAsync(
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _context.Orders.Where(o => o.UserId == userId);

        if (statuses?.Length > 0)
            q = q.Where(o => statuses.Contains(o.Status));

        if (!string.IsNullOrWhiteSpace(sourceModule))
            q = q.Where(o => o.SourceModule == sourceModule);

        return await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.FinalAmountMinor,
                o.Status.ToString(),
                o.SourceModule,
                o.Items.Count,
                o.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> CountUserOrdersAsync(
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        CancellationToken ct = default)
    {
        var q = _context.Orders.Where(o => o.UserId == userId);

        if (statuses?.Length > 0)
            q = q.Where(o => statuses.Contains(o.Status));

        if (!string.IsNullOrWhiteSpace(sourceModule))
            q = q.Where(o => o.SourceModule == sourceModule);

        return await q.CountAsync(ct);
    }

    public async Task<int> GetStoreVariantSoldQuantityAsync(
        Guid variantId,
        DateOnly? usageDate,
        StoreVariantCapacityScope capacityScope,
        Guid? excludeOrderId = null,
        CancellationToken ct = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(o =>
                o.SourceModule == "Store" &&
                o.PaymentState == PaymentState.Paid &&
                o.Status != OrderStatus.Cancelled &&
                o.Status != OrderStatus.Refunded);

        if (excludeOrderId.HasValue)
            query = query.Where(o => o.Id != excludeOrderId.Value);

        var items = await query
            .SelectMany(o => o.Items.Select(i => new
            {
                i.Quantity,
                i.MetadataJson
            }))
            .Where(i => i.MetadataJson != null)
            .ToListAsync(ct);

        var soldQuantity = 0;
        foreach (var item in items)
        {
            if (!TryReadStoreVariantMetadata(item.MetadataJson, out var metadataVariantId, out var metadataUsageDate))
                continue;

            if (metadataVariantId != variantId)
                continue;

            if (capacityScope == StoreVariantCapacityScope.PerEligibleDay &&
                metadataUsageDate != usageDate)
            {
                continue;
            }

            soldQuantity += item.Quantity;
        }

        return soldQuantity;
    }

    private static bool TryReadStoreVariantMetadata(
        string? metadataJson,
        out Guid variantId,
        out DateOnly? usageDate)
    {
        variantId = Guid.Empty;
        usageDate = null;

        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("variant_id", out var variantValue) ||
                variantValue.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(variantValue.GetString(), out variantId))
            {
                return false;
            }

            if (root.TryGetProperty("usage_date", out var usageDateValue) &&
                usageDateValue.ValueKind == JsonValueKind.String &&
                DateOnly.TryParse(usageDateValue.GetString(), out var parsedUsageDate))
            {
                usageDate = parsedUsageDate;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
