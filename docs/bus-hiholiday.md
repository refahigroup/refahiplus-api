# Bus discovery with HiHoliday

Bus discovery reads regions, services, prices and seats from HiHoliday over HTTPS. The API exposes Refahi References city IDs and short-lived offer IDs. It resolves domestic cities by exact normalized city and province names. Review any ambiguous or missing mappings and add an override in `bus.hiholiday_city_mappings` using the IDs from the two systems. An override must still point to a current HiHoliday city.

## Local access

Set `Bus:Enabled` and `Bus:HiHoliday:BaseUrl` in the API's ignored `appsettings.Local.json` and add the credentials in that same local file. Run the API with the `Refahi.Host (local)` launch profile. The Bus module applies its own migrations when enabled. Run the web host with its Local launch profile. Local web launch profiles already set `Bus__ShowDiscovery=true`. If running the web host without a launch profile, set that variable on the web host separately. The web host uses the existing API base address configuration and serves `https://localhost:5271/trip/bus` under the Local profile.

The route uses the live price supplied by HiHoliday and shows only seats marked Free as selectable. Seat selection currently previews the total. Bookings, checkout, issuance and refunds are pending.

## Live smoke test

From the API repo, set `BUS_SMOKE_SETTINGS` to the absolute path to the ignored local API settings file and `BUS_SMOKE_ORIGIN` and `BUS_SMOKE_DESTINATION` to two city names from HiHoliday. Run `dotnet test tests/Refahi.Modules.Bus.Tests`. The test selects its dates relative to Tehran time, resolves supplier city IDs from Regions and uses a current service from Search to test Detail. It performs read-only operations.

The API key enables discovery. Configure the merchant username and password under `Bus:HiHoliday:Username` and `Bus:HiHoliday:Password` in the ignored local API settings to verify the purchase contract. Verify `Balance` and a real-seat `Revalidate` before using the returned session for `PreReserve`. Confirm hold duration, issuance and uncertain-write reconciliation with HiHoliday before enabling checkout. Keep the secret out of telemetry and proxies because the provider protocol puts it in the URL path.
