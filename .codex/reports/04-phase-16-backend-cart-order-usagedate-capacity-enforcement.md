Summary:
- Implemented backend Store cart/order UsageDate support for SessionBased variant-capacity items.
- Added Store-to-Orders sold-quantity query through Orders Application.Contracts and Orders-owned read service.
- Enforced variant capacity in add-to-cart, cart sync, cart projection, cart quantity update, order creation, and paid-event defensive recheck.

Skills used:
- architecture-guardian
- order-wallet-guardian
- module-owned-order-detail

Repository checked:
- C:\Workspace\repo\refahiplus-api

Git state:
- Pre-check:
  - Existing dirty Store changes were present before this phase in cart/order/product handlers and Product/ProductVariant domain files.
  - Existing untracked file was present: .codex/prompts/04-phase-16-backend-cart-order-usagedate-capacity-enforcement.md.
- Final:
  - Modified backend Store/Orders files plus new Orders query, Store capacity helper, Store cart UsageDate migration, and this report.
  - The pre-existing dirty Store product/variant changes and prompt file remain preserved.

Files changed:
- src/Refahi.Modules.Store.Application.Contracts/Commands/Cart/AddToCartCommand.cs
- src/Refahi.Modules.Store.Application.Contracts/Commands/Cart/SyncCartCommand.cs
- src/Refahi.Modules.Store.Application.Contracts/Dtos/Cart/CartDto.cs
- src/Refahi.Modules.Store.Domain/Aggregates/Cart.cs
- src/Refahi.Modules.Store.Domain/Entities/CartItem.cs
- src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/CartItemConfiguration.cs
- src/Refahi.Modules.Store.Infrastructure/Migrations/20260623235000_Store_CartItem_AddUsageDate.cs
- src/Refahi.Modules.Store.Infrastructure/Migrations/StoreDbContextModelSnapshot.cs
- src/Refahi.Modules.Store.Application/Services/StoreVariantCapacityService.cs
- src/Refahi.Modules.Store.Application/Services/StoreSalesModelRules.cs
- src/Refahi.Modules.Store.Application/Features/Cart/AddToCart/AddToCartCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/Cart/SyncCart/SyncCartCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/Cart/GetCart/GetCartQueryHandler.cs
- src/Refahi.Modules.Store.Application/Features/Cart/UpdateCartItem/UpdateCartItemCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/Checkout/PlaceStoreOrder/PlaceStoreOrderCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/Checkout/FinalizeStoreOrder/StoreOrderPaidEventHandler.cs
- src/Refahi.Modules.Orders.Application.Contracts/Queries/GetStoreVariantSoldQuantityQuery.cs
- src/Refahi.Modules.Orders.Application.Contracts/Repositories/IOrderQueryService.cs
- src/Refahi.Modules.Orders.Application/Features/GetStoreVariantSoldQuantity/GetStoreVariantSoldQuantityQueryHandler.cs
- src/Refahi.Modules.Orders.Infrastructure/Repositories/OrderQueryService.cs
- Pre-existing dirty files preserved and built with this phase:
  - src/Refahi.Modules.Store.Domain/Aggregates/Product.cs
  - src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs
  - src/Refahi.Modules.Store.Application/Features/Products/AddProductVariant/AddProductVariantCommandHandler.cs
  - src/Refahi.Modules.Store.Application/Features/Products/AdminGetProduct/AdminGetProductQueryHandler.cs
  - src/Refahi.Modules.Store.Application/Features/Products/GetProductBySlug/GetProductBySlugQueryHandler.cs

Cart contract/domain/persistence changes:
- AddToCartCommand and SyncCartItemInput now accept nullable DateOnly UsageDate.
- CartItemDto now exposes nullable DateOnly UsageDate.
- CartItem stores nullable UsageDate.
- Cart merge identity now includes UsageDate, so SessionBased variant-capacity items merge only for the same shop, product, variant, null SessionId, and normalized usage date.
- StockBased and legacy ProductSession cart rows normalize UsageDate to null.

UsageDate normalization and validation:
- Added StoreVariantCapacityService.
- Single-day variants infer UsageDate from FromDate when omitted.
- RequiresUsageDate variants reject missing dates.
- Provided UsageDate must be within FromDate/ToDate.
- UsageDate is rejected for variants without a validity range.
- Existing ProductVariant.ValidateOrderEligibility(...) remains the domain validation entry point.

Orders sold-count query:
- Added GetStoreVariantSoldQuantityQuery in Orders Application.Contracts.
- Added StoreVariantCapacityScope in Orders Application.Contracts instead of referencing Store Domain from Orders contracts.
- Added Orders application handler delegating to IOrderQueryService.
- Added Orders Infrastructure read-service implementation that counts Store order item quantities from paid, non-cancelled, non-refunded orders by metadata variant_id and, for PerEligibleDay, usage_date.

Capacity enforcement:
- AddToCart validates SessionBased variant-capacity selected variants, normalizes UsageDate, queries runtime sold count, and calls ProductVariant.EnsureCapacityAvailable(...).
- SyncCart validates the same path and drops invalid/insufficient items with warnings.
- GetCart includes UsageDate and computes variant-capacity availability using runtime sold count.
- UpdateCartItem now validates new quantity against stock/session/capacity paths.
- PlaceStoreOrder revalidates capacity before creating the Order.
- StoreOrderPaidEventHandler defensively rechecks capacity after payment while excluding the current order.

StockBased behavior preserved:
- Product.StockCount and ProductVariant.StockCount remain authoritative for StockBased products.
- StockBased variants still use ProductVariant.StockCount and still decrement stock in the paid event handler.
- StockBased cart rows do not store UsageDate.

Legacy ProductSession behavior preserved:
- SessionId remains supported.
- ProductSession validation and ProductSession.Sell(...) paid finalization remain in place.
- Legacy SessionBased rows do not store UsageDate.

Order metadata / paid event changes:
- SessionBased variant-capacity order metadata now includes sales_model, variant_id, usage_date, capacity_type, from_date, and to_date.
- No fake session_id is written for variant-capacity items.
- Paid event no longer decrements ProductVariant.StockCount for SessionBased variant-capacity items.
- Added TODO for replacing paid-event sold-count recheck with atomic reservation/ledger before high-volume capacity sales.

Migration:
- Added Store migration 20260623235000_Store_CartItem_AddUsageDate.
- Added nullable date column store.cart_items.UsageDate.
- Updated StoreDbContextModelSnapshot.
- Migration was not applied to a real database.

Validation:
- Command: dotnet restore .\Refahi.Backend.slnx
  Result: Passed. Existing PaymentGateway System.Security.Cryptography.Xml 9.0.0 vulnerability warnings were reported.
- Command: dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
  Result: Passed. Existing warnings remain in PaymentGateway, Store.Api ShopProductsEndpoints nullable calls, and Refahi.Api version/nullability.
- Command: dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
  Result: Passed. Flights tests: 11 passed. PaymentGateway tests: 25 passed.

Manual QA:
- Not run. No local API database/test data was available for manual stock/session/variant-capacity purchase flows.

Deferred items:
- Full high-volume atomic capacity reservation/ledger is deferred.
- Sold-count query currently parses paid Store order metadata and is suitable for low-volume safety, not high-volume reporting/projection scale.
- No holiday, weekday, even/odd day, or excluded-date logic was added.
- No product detail remaining-capacity display was added.

Risks / assumptions:
- Sold quantity is counted from Orders with PaymentState.Paid and OrderStatus not Cancelled or Refunded.
- Paid-event capacity failure after wallet capture remains a business risk until pre-capture atomic reservation exists.
- Existing Store.Application still has a pre-existing direct Orders.Domain dependency for OrderPaidEvent handling; this phase did not broaden that boundary.
- Existing clients omitting UsageDate should continue to deserialize with null.

Suggested next phase:
- Implement atomic pre-payment capacity reservation for SessionBased variant-capacity items and move sold-count reads to an indexed projection or ledger.
