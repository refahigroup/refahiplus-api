# PoolTicket organizational v1

Updated 2026-11-03. The supplied, tested Bruno collection is the current wire-contract reference. All provider operations now use `/api/v1/org/...`, with `/api/v1/token` for organization authentication. See [the complete comparison and Local evidence](poolticket-bruno-alignment.md).

The session list sends `pool`, `gender`, `client_id`, `access_token` in a form POST. Both genders must succeed before publication. Flat `saens_row_id` rows retain public GUIDs when the existing venue+row identity is unambiguous. Identical rows returned for both genders normalize to one shared row; conflicting or repeated rows abort publication. An empty internal group key represents the absence of provider groups.

Explicit Gregorian local datetimes are interpreted in Asia/Tehran; ISO offsets/Z are respected. Invalid explicit times fail closed. The list exposes numeric capacity; the info endpoint accepts documented boolean availability and the numeric capacity actually returned by staging, normalized only to availability. Live explicit times flow into purchase revalidation, so a changed upstream time blocks payment.

Token acquisition is server-side and coordinated across concurrent requests. The documented `password` and `check_token` grants are supported; no refresh-token grant or public v2 fallback exists. An explicit invalid-token rejection on a read can reacquire once; reserve has no automatic retry. Secrets stay in ignored Local settings or a protected configuration provider.
