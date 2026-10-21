# Local media testing

The local API and web application run in Development with the trusted localhost HTTPS certificate.

- API / Swagger: https://localhost:5001/api/swagger/index.html
- Web: https://localhost:5271/trip/hotels
- Database: `refahi-media-local` in the existing local `postgres` Docker container, port 5432.
- Disposable image cache: `artifacts/media-local/cache`, 128 MiB, with a 2 GiB free-space floor.
- Image size limit: 5 MiB. Sliding expiration: 5 hours. Database activity flush: up to 5 minutes.

The database was prepared from the schema and migration history of the existing local `refahi-db`, copying only reference/geography data and hotel city/province mappings. Transactional records, users, payments, reservations and supplier credentials were not copied. The new Media migration is applied in this isolated database. The original database and its migration history are unchanged.

## Start again

Run these in separate PowerShell terminals from the backend repository:

```powershell
./scripts/media/start-local-api.ps1
```

```powershell
./scripts/media/start-local-web.ps1
```

Stop each process with Ctrl+C in its terminal. Scripts refuse occupied ports. If code changes, stop the relevant process and rebuild `Refahi.Backend.slnx` or the frontend's `Refahi.Clients.Solution.sln` before restarting. Scripts use existing Debug build output. Environment overrides apply to their PowerShell process and child app only, not machine settings or Production files.

Both hotel providers' purchase capabilities are disabled. Unconfigured Commerce providers are disabled for this session; SMS notifications use in-memory delivery. Hotel search and image GET requests are real read-only supplier calls. Lamaso is not configured in Development; a complete Lamaso search test requires its real read credentials and approved image hostnames.

## Sample images

After starting the API:

```powershell
./scripts/media/seed-local-samples.ps1
```

This idempotently registers the two public sample URLs supplied for this task, exclusively in `refahi-media-local`, under Module=LocalMediaTest and Provider=SnappTrip. It prints their local public URLs. It does not create an unauthenticated registration API.

- Gallery sample (`.../galleries/hotel/2920/52092.jpeg`): about 123 KiB; verified successful local GET 200, HEAD 200 and conditional GET 304.
- Cover sample (`.../3233/1b3d9c81-59d3-4ab6-9de8-a1a9402d1c34.jpeg`): about 8.4 MiB; expected rejection (502) under the agreed 5 MiB cap. This is a size-limit test, not an unavailable source. The frontend should display its local fallback.

## Verified full flow

A real Tehran search for 2026-10-23 to 2026-10-24 (reference city ID 354, two adults) returned 148 offers. Its API JSON and SSR HTML contained local media URLs and no `store.snapptrip.com` source URL. Repeat with future dates when these dates expire.

Status/last-access changes appear in `media.external_sources` after the five-minute activity flush. Cache bytes are local and can be examined under the directory above. Never clear the uploaded MediaStorage directory or the original refahi-db for this test.
