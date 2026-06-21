# Rendering Governance Expert

Enforces SSR/WASM governance.

Key rules:
- SSR for discovery/SEO pages
- WASM for transactional/authenticated pages
- InteractiveWebAssembly must use prerender:false
- InteractiveServer forbidden unless explicitly approved
- Every route must be added to page-rendering-inventory.md
