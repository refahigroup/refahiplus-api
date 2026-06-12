# AGENTS.md — Refahi Plus Engineering Constitution

> Primary engineering rules and operational context for all AI agents and developers working on Refahi Plus.
>
> This document is mandatory reading before any analysis, planning, refactor or implementation.

---

# 1. Project Identity

Refahi Plus is a modular B2B2C welfare super-application.

The platform provides services including:

- Store / Marketplace
- Hotel reservation
- Flight reservation
- Restaurant
- Mobile charge
- Wallet & payment
- Organizational welfare credits

Core business model:

Organizations allocate welfare credits to employees through organizational wallets. Users consume services/products using wallet balance.

---

# 2. Core Architecture

## Backend Architecture

- Modular Monolith
- Domain-driven module separation
- Schema-per-module
- CQRS via MediatR
- Minimal APIs
- EF Core for write
- Dapper optional for heavy reads
- PostgreSQL
- Redis caching
- FluentValidation
- Polly resilience

## Frontend Architecture

- Blazor Web App (.NET 10)
- Hybrid SSR + Interactive WebAssembly
- Shared design system
- Modular UI architecture
- WASM islands for localized interactivity

---

# 3. Mandatory Reading Order

Before touching any module:

1. Read:
   - docs/01-refahi-overview.md
   - docs/02-refahi-architecture.md

2. Read rendering governance:
   - docs/rendering-governance-constitution.md
   - docs/page-rendering-inventory.md
   - docs/rendering-architecture-migration-report.md

3. Study related existing modules before creating new ones.

Example:
- Flight module MUST study Hotels module first.
- Store checkout MUST study existing Checkout implementation first.

Never invent architecture in isolation.

---

# 4. Module Architecture Rules (STRICT)

Every backend module MUST contain exactly 5 layers/projects:

- Domain
- Application.Contracts
- Application
- Infrastructure
- Api

Naming convention:

Refahi.Modules.{Module}.{Layer}

Examples:

- Refahi.Modules.Hotels.Domain
- Refahi.Modules.Orders.Application.Contracts
- Refahi.Modules.Store.Api

---

# 5. Dependency Rules (NON-NEGOTIABLE)

## Allowed Internal Dependencies

Api
→ Application
→ Domain

Infrastructure
→ Domain

Application
→ Domain
→ Application.Contracts
→ Shared

Infrastructure
→ Application.Contracts
→ Shared

## Cross-module Communication

Modules may ONLY reference:

{OtherModule}.Application.Contracts

Direct references to:
- Domain
- Application
- Infrastructure
- Api

of another module are forbidden.

Cross-module operations must use:
- MediatR
- Contracts
- Shared abstractions

---

# 6. Data Ownership Rules

Each data entity has exactly one owner module.

Other modules:
- store only IDs
- or historical snapshots

Never duplicate business ownership.

Examples:

| Data | Owner |
|---|---|
| User | Identity |
| Wallet | Wallets |
| Order | Orders |
| Hotel Reservation | Hotels |
| Flight Booking | Flights |

---

# 7. Order & Wallet Rules (CRITICAL)

## Golden Rule

ONLY Orders are payable.

Wallet NEVER pays Hotel, Flight, Store, etc directly.

Flow:

Feature Module
→ Create Order
→ Orders Module
→ Wallet Payment

## Required Flow

Feature module:
1. Stores its own business data
2. Creates Order
3. Redirects to Checkout
4. Wallet pays Order

## Wallet Constraints

- Wallet operations are append-only
- All amounts are long (IRR minor unit)
- All payment operations MUST be idempotent
- Refunds must preserve original allocation

---

# 8. CategoryCode Rules

Wallet restrictions rely on CategoryCode.

Examples:

- hotel
- flight
- store
- store.clothing

Matching uses StartsWith semantics.

Never hardcode unrelated category logic.

---

# 9. Backend Development Rules

## Domain Layer

Must contain:
- Aggregate roots
- Entities
- Value objects
- Enums
- Domain events
- Repository interfaces

Rules:
- Private constructors
- Factory methods
- Private setters
- Domain behaviors instead of public mutation

## Application Layer

Contains:
- Commands
- Queries
- Handlers
- Validators

Feature structure:

Features/{Feature}/{Action}/

Example:

Features/Bookings/CreateBooking/

## Infrastructure Layer

Contains:
- DbContext
- EF configurations
- repositories
- provider clients
- migrations

Each module MUST use dedicated PostgreSQL schema.

## Api Layer

Minimal API endpoints only.

Every endpoint:
- implements IEndpoint
- uses ApiResponseHelper
- has WithName()
- has WithTags()

Error messages MUST be Persian.

---

# 10. API & Response Rules

Use unified API responses.

Never return raw objects.

All APIs must:
- validate input
- use FluentValidation
- support cancellation tokens
- support proper HTTP status codes

---

# 11. Frontend Rendering Constitution (MANDATORY)

## Render Mode Strategy

### SSR

Use SSR for:
- landing pages
- SEO pages
- discovery pages
- product detail pages
- catalog pages

SSR pages MUST NOT use page-level @rendermode.

### Interactive WebAssembly

Use WASM for:
- authenticated pages
- transactional flows
- checkout
- profile
- cart
- orders
- wallet
- login/register

### WASM Islands

Use WASM islands for:
- localized interactions
- add-to-cart
- login modal
- search cards
- cart badge
- reviews

## Forbidden

NEVER use:
- InteractiveServer
- InteractiveAuto

unless explicitly approved by architecture decision.

---

# 12. Mandatory WASM Rule

ALL InteractiveWebAssembly components/pages MUST use:

@(new InteractiveWebAssemblyRenderMode(prerender: false))

Example:

@rendermode @(new InteractiveWebAssemblyRenderMode(prerender: false))

Reason:
- prevent double lifecycle execution
- prevent duplicated API calls
- avoid hydration flicker

---

# 13. Discovery Page Rules

Discovery/search pages:
- MUST be SSR
- MUST store filter state in URL query string
- MUST NOT rely solely on component state

Examples:
- hotels
- store product lists
- shop lists
- category pages

---

# 14. Auth Rules

- Auth services must remain browser-scoped
- NEVER singleton auth state
- JWT + browser storage currently coexist with SSR cookie sync
- Login flow must execute from browser/WASM path

---

# 15. Store Rules

Store discovery pages:
- SSR

Product detail:
- SSR

Add-to-cart:
- WASM island

Cart & checkout:
- WASM pages

Checkout drafts:
- sessionStorage only

---

# 16. Hotel Rules

/hotels:
- SSR landing

Search card:
- WASM island

Hotel search results:
- SSR with URL-driven filters

Hotel checkout:
- WASM

---

# 17. Flight Module Rules

Flight module MUST follow Hotel architectural patterns.

Required:
- Provider-based architecture
- Provider abstraction
- Order integration
- Wallet integration via Order only
- Store provider IDs and captions
- Preserve provider traceability
- Support future providers

Provider responses may be snapshotted for auditability.

Never tightly couple Flight module to one provider.

---

# 18. Frontend Reuse Rules

Before creating new UI:

1. Check:
   - Refahi.Clients.Web.UI
   - theme/default
   - existing feature modules

2. Reuse:
   - components
   - layouts
   - patterns
   - styles
   - form components

Avoid duplicate UI code.

---

# 19. Design Rules

UI implementation MUST follow approved design assets.

Never invent unrelated layouts.

Required analysis:
- user flow
- form states
- validation states
- loading states
- responsive behavior

---

# 20. Rendering Inventory Rule

Every new page/route MUST be registered in:

docs/page-rendering-inventory.md

No exceptions.

Every PR introducing a page must specify:
- route
- render mode
- SSR/WASM rationale

---

# 21. PWA Rules

- API requests: network-only
- navigation: network-first
- static assets: cache-first allowed
- verify service worker after WASM changes

---

# 22. Performance Rules

Prefer:
- SSR for first meaningful paint
- URL-driven state
- lightweight islands
- component reuse
- lazy loading

Avoid:
- unnecessary hydration
- global interactivity
- duplicate API requests

---

# 23. Logging & Observability

All transactional flows should support:
- structured logging
- provider trace IDs
- order correlation IDs
- payment correlation IDs

Provider integrations should log:
- request
- response
- retries
- failures
- latency

Sensitive data must be masked.

---

# 24. Security Rules

Never:
- trust provider payload blindly
- expose raw provider errors to users
- store secrets in source code
- bypass authorization

Always:
- validate provider responses
- validate user ownership
- enforce authorization policies
- sanitize external payloads

---

# 25. Coding Standards

Prefer:
- explicit naming
- feature folders
- immutable contracts
- small handlers
- thin endpoints

Avoid:
- god services
- hidden shared state
- direct DbContext usage across modules
- duplicated business logic

---

# 26. Planning & Analysis Rules for AI Agents

Before implementation:

1. Read all referenced documents
2. Study related modules
3. Study existing rendering rules
4. Analyze existing patterns
5. Ask clarifying questions if ambiguity exists
6. Produce implementation plan BEFORE coding

If a file/path is missing:
- explicitly report it
- do not hallucinate content

---

# 27. Forbidden Actions

Never:
- introduce InteractiveServer casually
- bypass Orders for payments
- connect Wallet directly to feature modules
- introduce cross-module tight coupling
- duplicate auth systems
- invent parallel UI frameworks
- ignore rendering governance
- skip inventory registration
- create duplicate checkout flows

---

# 28. Definition of Done

A feature is NOT complete unless:

- architecture rules respected
- render mode documented
- inventory updated
- build passes
- provider abstraction implemented
- Order integration verified
- Wallet flow verified
- logging added
- validation added
- loading/error states handled
- reusable UI considered
- documentation updated

---

# 29. Recommended Workflow for AI Agents

For any new module:

1. Read overview docs
2. Read architecture docs
3. Read rendering governance
4. Analyze related module
5. Analyze existing checkout/order flow
6. Analyze designs
7. Produce architecture proposal
8. Produce implementation plan
9. Ask questions
10. Only then begin coding

---

# 30. Important Project Context

Primary repositories:

Backend:
C:\Workspace\repo\refahiplus-api

Frontend:
C:\Workspace\repo\refahiplus-webapp

Documentation:
C:\Workspace\Refahi\docs

Always verify paths/files before relying on them.

If something cannot be found:
- report clearly
- continue with grounded assumptions only
