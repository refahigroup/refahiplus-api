# Diagnosing vendor in-person order conflicts

`POST /api/store/vendor/in-person-orders` uses `InPersonTypedErrorFilter`.
An informational ASP.NET log naming `InPersonErrorResponse` and HTTP 409 means
a Store domain rejection was handled; it does not identify its cause.
Read the response JSON `code` and `message` before changing business behavior.

The filter now emits a Warning with `ErrorCode`, `StatusCode`, endpoint display
name and `TraceIdentifier`. It does not log request bodies, query strings,
exception messages, mobile numbers, OTPs or protected OTP references.

Relevant start-request conflicts:

- `AGREEMENT_TERM_NOT_EFFECTIVE`: no effective in-person agreement term resolves
  for the supplier, product category and current UTC time. Inspect the agreement
  and category/channel eligibility; do not bypass the contract requirement.
- `IDEMPOTENCY_PAYLOAD_MISMATCH`: the same buyer/vendor request key was reused
  with a different fingerprint (actor, buyer, shop, product or amount).
- `IDEMPOTENCY_CONFLICT`: persistence detected an already-used request key and
  the handler could not recover the matching order.
- `OTP_DELIVERY_REQUIRES_RESEND` (before the recovery fix): an order exists with
  OTP dispatch started but no persisted challenge. The dispatch marker is saved
  before calling the notification service; a failure or interruption before the
  protected challenge is persisted leaves this state. The code alone does not
  distinguish notification failure, protection/persistence failure or interruption.
- `OTP_DISPATCH_INVALID`: the StoreOrder is not eligible to begin OTP dispatch.

Do not delete orders, clear dispatch markers, rotate request keys blindly or
disable idempotency to silence a conflict. A production incident cannot be
assigned to one of these causes from the HTTP completion log alone.

## Recovery fix

Previously, retrying the start request returned 409 without an order ID, leaving
the vendor panel on the confirmation step with no usable resend button.
Start now returns the existing order with a null `otpReferenceCode` after the
same fingerprint and current vendor authorization checks. It does not send
another OTP, reset the dispatch marker, create another order or pay anything.
The existing nullable challenge contract is retained.

The vendor panel displays an uncertain-delivery message for this state, disables
payment verification until a reference exists and enables explicit resend.
Resend uses the existing authorized endpoint and attaches a new protected
challenge to the same StoreOrder. A successful challenge is reused on subsequent
start retries. Existing API rate limiting remains in place.

Deploy both the API and vendor panel. The vendor service-worker cache version is
bumped to refresh the WASM assets. API requests remain network-only and navigation
remains network-first. No migration, new route or render-mode change is required.
Retry the original request using the original payload and idempotency key, then
use explicit resend on the recovered order. No production requests were executed
during verification; the cause of the original interrupted dispatch still needs
the earlier production exception/notification logs.

Validation: `InPersonOtpRecoveryTests` covers repeat recovery without another
send, simulated initial send failure followed by explicit resend, changed-payload
rejection and revoked vendor permission. `InPersonTypedErrorFilterTests` verifies
structured rejection logs and retained response behavior without sensitive data.
