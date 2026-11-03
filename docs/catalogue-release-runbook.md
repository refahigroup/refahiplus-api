# Catalogue release and rollback

The release target is one API replica behind Nginx, with Hotels and Entertainments enabled. Partialness is operational metadata only; neither the home search nor the SSR page displays a partial-results warning. Stage keys in appsettings are intentional. Production keys are supplied on the server.

## Application limits

`Catalogue:MaxConcurrentRequests=8`, `MaxConcurrency=16`, source deadline 3s, total deadline 5s. The existing client deadline is 8s. Local's existing 6/7s overrides remain local. There is no admission queue and no automatic retry. All three Catalogue routes share `CatalogueAdmission`; rejected requests receive unified Persian JSON, 429, `Retry-After: 1`, and `Cache-Control: no-store`.

At startup, the source execution budget must cover admitted requests multiplied by enabled source count. A third source requires a capacity review. Do not increase these values merely to hide a failed performance test. `Refahi.Catalogue/catalogue.rejections` records the bounded reason `concurrency`. Nginx's safe log records `rate` rejections; query text, coordinates and IP are not metric labels.

## Nginx installation

1. Obtain the actual running `nginx -T` configuration, the Web host's **socket peer IP as observed by Nginx**, and current firewall/container port bindings. Never infer the trusted IP from `X-Forwarded-For`.
2. Ensure the Web peer is distinct from public traffic peers. If a NAT/router makes them indistinguishable, give internal SSR traffic a distinct private route before enabling the exemption. Do not exempt the NAT gateway, a subnet, or a caller-controlled header.
3. Render the exact IP:

```sh
python3 tools/render-catalogue-nginx.py \
  --web-peer-ip "$VERIFIED_WEB_PEER_IP" \
  --output /etc/nginx/snippets/catalogue-http.conf
```

4. Include the rendered file inside `http {}`. Include `deployment/nginx/catalogue-errors.conf` inside each affected `server {}`. Include `catalogue-api-location.conf` inside the existing `/api/catalogue/` proxy location and `catalogue-page-location.conf` inside the exact `/catalogue` proxy location. Preserve their existing upstream/proxy/header settings. The snippets deliberately do not add `proxy_set_header`, which would override inherited Host/protocol headers. Do not add duplicate locations.
5. Both routes share one 20r/s-per-IP bucket with burst 40 and `nodelay`. Only the exact Web peer's API calls have an empty bucket key; `/catalogue` itself is never exempt. API-wide admission still protects internal requests.
6. Run `nginx -t`, then reload using the deployment's existing service command. Keep a copy of the prior config. The API port must remain private; publish only Nginx.
7. Test both public paths, forged forwarding headers, the shared bucket, trusted SSR API calls and log privacy. A local disposable regression is available:

```sh
python3 tools/test-catalogue-nginx.py
```

Its synthetic loopback peer verifies the mechanism, not the production IP. Inspect `artifacts/catalogue-release/nginx-test.json`. The log `/var/log/nginx/catalogue.json` contains path, status, duration and rejection reason. Ensure other proxy/WAF/access logs also exclude query strings. The snippets suppress Nginx rejection error messages containing the original query.

## Dependency and runtime evidence

Pinned fixes: Microsoft.OpenApi 2.7.5; Cryptography.Pkcs and Cryptography.Xml 10.0.13. Keep build-tool PrivateAssets and do not suppress NuGet audit warnings. The Web host's WebAssembly.Server package must also be serviced to 10.0.13 because the whole-Web audit identifies its debugger vulnerabilities.

Both runtime Dockerfiles and the generated GitLab runtime Dockerfiles reject ASP.NET Core older than 10.0.13. The private `aspnet:10.0` image must therefore be serviced before an image build can pass. Update the private build image as appropriate; do not remove the gate to make an old image pass. Docker jobs save runtime versions, the built image ID and runtime-image RepoDigests. Registry publication retains the normal tagged/digest identities.

Run the audit on the actual restored graph and inspect Linux publish dependencies:

```sh
dotnet list Refahi.Backend.slnx package --vulnerable --include-transitive --format json > artifacts/catalogue-release/security-audit.json
python3 tools/verify-catalogue-release.py \
  --audit artifacts/catalogue-release/security-audit.json \
  --trx artifacts/catalogue-release/test-results \
  --deps publish/api/Refahi.Api.deps.json \
  --require-integration --output artifacts/catalogue-release/evidence.json
```

The verifier understands NuGet's omitted-framework representation of a clean project. Missing/malformed evidence, feed diagnostic logs, moderate/high/critical vulnerabilities, unapproved published versions, failed tests or missing/skipped required PostgreSQL fixtures fail the release. Existing nullable warnings alone do not fail it.

## Read-only coverage check

Run with the **same appsettings directory, ASPNETCORE environment name and environment-variable overrides as the deployed API**. This includes the same enabled sources, provider flags, account identity, read scopes and freshness values. The runner reproduces those JSON/environment inputs; it does not read arbitrary custom configuration providers. If deployment adds another provider or command-line configuration, export its effective Catalogue/Hotels/Entertainments overrides into the runner environment first.

Set PostgreSQL's standard `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD` through the existing protected server environment/secret mechanism. Confirm they identify the application's actual database. Use a role with SELECT permission only. Never put passwords on the command line or commit them in the report.

```sh
python3 tools/verify-catalogue-readiness.py \
  --config-dir /app --environment "$ASPNETCORE_ENVIRONMENT" \
  --output artifacts/catalogue-release/coverage.json
```

This requires `psql` and Python 3. On a local machine without psql, `--docker-container NAME` uses an existing container's client and forwards only PG variables. It does not create, reset or migrate a database. For active local-content read scopes, also supply `--api-url https://STAGE_ORIGIN` and set `REFAHI_READINESS_ADMIN_TOKEN` through the protected environment; the existing AdminOnly Hotels readiness endpoint checks content/images. Missing admin evidence fails closed.

The SQL uses one repeatable-read, READ ONLY transaction and a 30s statement timeout. Output is JSON plus a console table, exit 0 only if ready. A valid successful empty inventory is ready. Missing sync, failed sync, expired data, pending details and invalid active mappings are failures. No coordinates and unmapped entertainment services are reported separately; they do not alone imply failure. Inactive hotel geography and disabled Lamasoo mappings are excluded, matching the reader. Historical TouristPanel accounts are excluded.

Remediation must follow each row's reason:

| Reason | Action |
|---|---|
| `missing_sync` | Check provider configuration, geography and worker scheduling; let the existing worker finish its full sweep. |
| `sync_failed` | Inspect masked provider status/error type and trace; fix contract/configuration/native mapping and allow the existing retry. |
| `pending_details` | Inspect detail-fetch and Media failures; fix the failing dependency and let details refresh. |
| `expired_data` | Restore successful scheduled refresh; do not widen retention to conceal it. |
| `invalid_mapping` | Correct reviewed mappings through existing management paths; do not guess geography from captions. |
| `missing_local_content_readiness` | Supply existing admin readiness evidence for the active local-content scopes. |

Do not directly update LastSuccess/Observed timestamps, Status, Published or IsPartial to pass the gate. Do not drop/rename historical tables or regenerate public keys. Re-run coverage, per-source/per-scope API reads and representative provider detail links after remediation. The `/filters/locations` ready status alone is not data-readiness evidence.

## CI and database safety

GitLab is the release authority. Merge Requests run validation; only pushes to main/staging/dev build/push images and notify deployment. GitHub workflows are manual build checks without registry publication.

Backend CI runs Wallets, Catalogue, Hotels, Entertainments, Orders, Flights, Store and SupplyChain. PostgreSQL 16 is disposable. Explicit Hotel/Entertainment connections use job-specific database names. `CI=true` plus `REFAHI_DISPOSABLE_CI_DATABASE=true` permits only the CI host `postgres:5432`; local Entertainment fixtures still require loopback:55442. Other hosts, invalid prefixes and insufficient CREATE DATABASE permission fail before fixture mutation. There is no fallback to application settings.

The two historical migration fixtures must appear as Passed in TRX. They verify upgrade/replay and identity preservation. Wallets receives its explicit existing test connection too. Backend evidence verification must succeed before Docker publication.

Web CI retains all solution and Node tests and adds a published-output Chromium job at 390px/1440px with service workers allowed/blocked. It covers both sources, SSR, cancellation/history/pagination, empty results, partial responses without warnings, 429, 503 and timeout/manual retry. Expected error responses must not trigger automatic retries. Fixtures use no live supplier credentials.

## Deferred Stage load test

**Execution is intentionally deferred at the user's request.** The checked-in k6 scenario remains for a later approved Stage window; it is not part of automatic CI and must not run against production by default.

```sh
CATALOGUE_LOAD_URL=https://STAGE_ORIGIN \
CATALOGUE_RELEASE_COMMIT="$RELEASE_COMMIT" \
CATALOGUE_RELEASE_IMAGE="$RELEASE_IMAGE_DIGEST" \
k6 run tools/catalogue-load.js
```

Optional `CATALOGUE_LOAD_CITY`, `CATALOGUE_LOAD_LAT`, `CATALOGUE_LOAD_LON` select reviewed Stage geography. The fixed mix is 60% text/geography (including 10% total through SSR), 20% nearby, 10% pagination and 10% location filters. With 50 VUs: warmup 2m at 20r/s, baseline 15m at 20r/s, burst 2m at 40r/s, recovery 30s at 20r/s; 10s drain gaps keep phases from overlapping. Save `catalogue-load-summary.json` with the release evidence.

Baseline gates: p95 <=2s, p99 <=4s, unexpected failures <1%, no capacity rejection, no partial API responses and no dropped scheduled iterations. Burst may return controlled 429 but unexpected failures remain <1%. Recovery must return to normal within the 30s observation window. Verify process/DB stability with the existing infrastructure monitoring as well; k6 does not certify server memory itself. Single-generator traffic also exercises a shared NAT IP.

## Release and rollback

Publish only after security evidence, both DB fixtures, coverage and browser verification pass. The deferred Stage load test and a 24h deployment observation remain explicit operational acceptance steps. Record the exact Nginx configuration, Web peer, commit/image identity, 429/503 rates, response latency and sync health. Configure the existing monitoring stack to consume the Catalogue meter and safe Nginx log; emitting a Meter alone is not proof of collection.

Keep Catalogue off in the base configuration. Enable API and Web flags only for the intended deployment, retaining both source keys and the chosen workers. On incident, disable Catalogue flags or restore the prior compatible image/Nginx configuration. Keep additive schema/history and all public/transactional identities. Never use schema downgrade or data deletion as rollback.

## Pools organizational source

The third source is `pools`; follow [PoolTicket Bruno alignment](poolticket-bruno-alignment.md). With eight admitted requests, set MaxConcurrency >=24. Include `sources=pools`, canonical province/city, nearby, confirmed-empty and stale/unavailable cases in readiness checks. Verify `poolticket_sync_states` completion and active-venue geographic coverage; unmatched native geography is reported separately. Local discovery is enabled for verification; real purchasing remains disabled pending a controlled upstream reservation.
