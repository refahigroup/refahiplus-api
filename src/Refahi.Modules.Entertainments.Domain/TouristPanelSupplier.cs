namespace Refahi.Modules.Entertainments.Domain;

public sealed class TouristPanelSupplier
{
    private TouristPanelSupplier() { }
    public Guid SupplierKey { get; private set; }
    public string AccountKey { get; private set; } = "";
    public string NativeId { get; private set; } = "";
    public static TouristPanelSupplier Create(string accountKey, string nativeId) => new()
    { SupplierKey = Guid.NewGuid(), AccountKey = accountKey, NativeId = nativeId };
}
