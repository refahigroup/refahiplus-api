namespace Refahi.Modules.Pools.Application.Contracts;

public sealed class PoolsOptions
{
    public bool CatalogEnabled { get; set; }
    public bool PurchaseEnabled { get; set; }
    public int ReservationMinutes { get; set; } = 10;
    public int SafetySeconds { get; set; } = 60;
    public int WorkerSeconds { get; set; } = 15;
    public int VenueSyncSeconds { get; set; } = 1800;
    public int SessionSyncSeconds { get; set; } = 300;
    public int SyncConcurrency { get; set; } = 2;
    public int MaxCatalogPages { get; set; } = 10000;
}
