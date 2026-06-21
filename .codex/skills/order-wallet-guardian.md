# Order & Wallet Workflow Guardian

Protects Order/Wallet architecture.

Golden rule:
Wallet ONLY pays Orders.

Required flow:
Feature Module -> Order -> Checkout -> Wallet

Forbidden:
- Direct Wallet access from Flight/Hotel
- Custom checkout implementations
