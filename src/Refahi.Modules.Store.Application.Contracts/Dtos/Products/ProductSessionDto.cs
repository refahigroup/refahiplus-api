namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

/// <summary>Date/Time stored as formatted strings for API consumers.</summary>
public sealed record ProductSessionDto(
    Guid Id, string Date, string StartTime, string EndTime,
    string? Title, int Capacity, int SoldCount, int RemainingCapacity,
    long PriceAdjustment, bool IsAvailable);
