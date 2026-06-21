# module-owned-order-detail.md

# Module-Owned Order Detail

Protects source-module ownership of order detail UI and data.

Rules:
- Orders module owns order summaries and order list projection.
- Source modules own their own order detail UI and detail data.
- Profile must not implement Flight/Hotel/Store detail UI directly.
- Profile must not duplicate source-module DTOs.
- Profile must not query source-module internals directly.
- Preferred UX is opening a source-module-owned bottom sheet.
- Alternative is navigation to a source-module-owned page.

Allowed providers in this phase:
- Flights
- Hotels
- Store

Forbidden:
- Restaurant detail
- Generic detail page containing module-specific rendering logic
- Direct references from Profile to source module internals

Preferred abstraction:
- IOrderDetailSurfaceProvider
- IOrderDetailSurfaceRegistry

Each source module registers its own provider.
Profile only resolves provider by SourceModule.