-- Run as the deployment administrator, separately from the immutable data release.
-- Target: refahi-usr. Use the API connection's actual role if it is different.
BEGIN;
GRANT USAGE ON SCHEMA flights, "references" TO "refahi-usr";
GRANT SELECT ON TABLE
    flights.snapptrip_city_mappings,
    flights.airport_geography_mappings,
    "references".countries,
    "references".provinces,
    "references".cities
TO "refahi-usr";
COMMIT;
