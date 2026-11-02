# In-person StoreOrder payment plan

The in-person Store handlers create an Orders order with `SourceModule=Store`,
`ReferenceType=StoreOrder`, and `SourceReferenceId=StoreOrder.Id`. Its item source
ID identifies the StoreOrderItem. Both user and vendor entry points use this
contract so the shared Store paid/cancelled lifecycle handlers can correlate it.

Orders previously accepted financial plans only for the legacy `StoreInPerson`
reference type, whose source reference is null. This rejected current requests
with `UNSUPPORTED_PAYMENT_PLAN` before checkout.

Orders now validates financial plans for both contracts. A current StoreOrder
with a snapshot or nonempty postings must have a nonempty source reference and
the complete plan. Single-item, quantity, discount, shipping, commission, VAT,
net amount and posting balance checks remain in force. Ordinary online StoreOrder
requests without a plan retain their existing behavior. Legacy StoreInPerson
orders retain their null-source-reference requirement.

No migration or frontend/render-mode change is required. Deploy the API fix;
retrying the original Store request with the same idempotency key resumes its
stored StoreOrder when Orders creation previously failed. Do not remove the
StoreOrder or change its key to recover this error.

Regression checks: `OrderFinancialPlanTests` covers both contracts and invalid
plans; `PayOrderCommandHandlerConcurrencyTests` checks that concurrent checkout
requests create and capture one financial intent for each contract. These tests
use local fakes and do not charge production wallets.
