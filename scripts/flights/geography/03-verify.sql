BEGIN READ ONLY;
SELECT version,digest,applied_at FROM flights.geography_releases ORDER BY applied_at;
SELECT count(*) AS total_airports,count(m."CityId") AS mapped_airports
 FROM flights.airports a LEFT JOIN flights.airport_geography_mappings m ON m."AirportIataCode"=a.iata_code;
SELECT a.country_code,count(*) AS remaining FROM flights.airports a
 LEFT JOIN flights.airport_geography_mappings m ON m."AirportIataCode"=a.iata_code
 WHERE m."CityId" IS NULL GROUP BY a.country_code ORDER BY 2 DESC,1;
SELECT count(*) AS selectable_memberships,count(m."CityId") AS mapped_memberships
 FROM flights.search_memberships s LEFT JOIN flights.airport_geography_mappings m ON m."AirportIataCode"=s.airport_code;
SELECT c.city_code,c.city_name_en,m."CityId",r."Name",p."CountryCode" FROM flights.search_cities c
 LEFT JOIN flights.snapptrip_city_mappings m ON m."ProviderCityCode"=c.city_code
 LEFT JOIN "references".cities r ON r."Id"=m."CityId"
 LEFT JOIN "references".provinces p ON p."Id"=r."ProvinceId" ORDER BY c.city_code;
SELECT a.iata_code,a.city_code,a.country_code,c."Name",p."CountryCode" AS actual_country
 FROM flights.airports a JOIN flights.airport_geography_mappings m ON m."AirportIataCode"=a.iata_code
 JOIN "references".cities c ON c."Id"=m."CityId" JOIN "references".provinces p ON p."Id"=c."ProvinceId"
 WHERE a.country_code<>p."CountryCode" OR a.iata_code='YXU';
COMMIT;
