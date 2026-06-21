# Architecture Guardian

Protects modular monolith architecture, layering, module boundaries, rendering governance and dependency direction.

Key rules:
- Only Application.Contracts cross-module
- No direct Domain references between modules
- Wallet only pays Orders
- Discovery pages remain SSR
- Transactional pages use WASM

## Profile / Order Detail Boundary

Profile may display order summaries from Orders module.

Profile must not own or implement module-specific order detail.

Order detail belongs to the source module:
- Flight detail belongs to Flights
- Hotel detail belongs to Hotels
- Store detail belongs to Store

Profile may delegate detail rendering through an approved abstraction/registry.