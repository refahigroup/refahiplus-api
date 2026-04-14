using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class CartItem
{
    private CartItem() { }

    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid? SessionId { get; private set; }    // v1.1 — برای محصولات SessionBased
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }

    internal static CartItem Create(
        Guid cartId, Guid productId, Guid? variantId,
        Guid? sessionId, int quantity, long unitPriceMinor)
        => new()
        {
            Id = Guid.NewGuid(),
            CartId = cartId,
            ProductId = productId,
            VariantId = variantId,
            SessionId = sessionId,
            Quantity = quantity,
            UnitPriceMinor = unitPriceMinor
        };

    internal void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        Quantity = newQuantity;
    }
}
