# PoolTicket organizational v1 session reads

Updated 2026-11-03. Session discovery now uses POST {Pools:PoolTicket:BaseUrl}/api/v1/org/saens/list. Other operations retain their existing routes; this change does not enable reservations or sales.

## Configuration

Keep credentials server-side. Set Pools__PoolTicket__AccessToken through the environment/secret mechanism, alongside the existing Pools__PoolTicket__ClientId, then restart the API. A missing token fails with POOLS_PROVIDER_AUTH_NOT_CONFIGURED before HTTP. Rejected/expired tokens map to POOLS_PROVIDER_TOKEN_INVALID and do not log the Refahi user out. No refresh grant, automatic token acquisition or v2 fallback is assumed.

The user-provided www.staging.poolticket.org hostname returned DNS ENOTFOUND in the local environment. The existing configured staging.poolticket.org hostname resolves. Read-only probes to its organizational v1 endpoint with the configured client ID and no organization token returned HTTP 400 / invalid_access_token for both gender=0 and gender=1, replacing the general v2 access_denied result. Do not disable TLS verification or hardcode DNS/IP overrides.

## Request and normalization

Form POST sends pool (native pool ID), gender, client_id and access_token, with Accept: application/json. Both documented genders are queried before publication. The response contains flat saens rows, not v2 groups/rows. Internally the group key is empty; saens_row_id remains the native row identity. Identical rows returned for both genders become one normalized row eligible for both; conflicting identities, duplicate rows within a gender, incomplete responses, invalid capacity or invalid explicit datetimes abort publication.

Explicit start_datetime/finish_datetime values support Gregorian ISO timestamps with offsets/Z, or exact Gregorian local yyyy-MM-dd HH:mm:ss / yyyy-MM-ddTHH:mm:ss interpreted in Asia/Tehran. Unknown explicit formats fail closed rather than guessing from the display time. If both explicit fields are absent, the existing date/range parser remains the fallback. These are tested accepted formats, not certification of an unseen live supplier payload.

Unique existing pool+row mappings retain their public GUID and historical group identifier. Ambiguous legacy mappings are rejected. Session sync no longer requires the general v2 terms endpoint: row terms are sanitized from v1, while the previous collection-level terms are retained. No database migration or public frontend DTO change is needed.

## Verification

Provider/normalization tests cover the exact v1 path/form, both genders, missing and rejected token, row identity, capacity, HTML safety and timezone conversion. Sync tests verify GUID retention, repeat refresh, ambiguous mapping rejection and absence of v2 terms calls. Live publication remains gated on a valid organization token and successful response validation; existing data is never retimed or fabricated to populate the UI.

Verification evidence: the non-PostgreSQL Pools run passed 85 tests before the additional ambiguous-empty-group case; the final focused sync run passed all 3 mapping scenarios after that case was added. The API host build passed with existing warnings and zero errors. No application migration, token acquisition, reserve or financial mutation was performed. No successful live session inventory is claimed: the organization token is absent in both the local config file and launch environment.
