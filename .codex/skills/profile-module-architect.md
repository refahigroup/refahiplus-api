# profile-module-architect.md

# Profile Module Architect

Owns Profile-area implementation planning.

Rules:
- Profile owns profile dashboard, edit profile, addresses, wallet screens, and order list UX.
- Profile does not own module-specific order detail.
- Profile must use real backend APIs only.
- Mock data, placeholder services, hardcoded business data, and temporary endpoints are forbidden.
- Disabled menu items must be visible but non-navigable.

Scope:
- Profile Dashboard
- Edit Profile
- Orders List
- Wallet Dashboard
- Wallet Transactions
- Wallet Top-Up
- Wallet Withdraw
- Addresses CRUD
- Empty States

Out of scope:
- Favorites
- Reviews
- Discounts & Rewards
- Restaurant Order Detail