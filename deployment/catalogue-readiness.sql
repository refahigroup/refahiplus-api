-- psql variable config is a SANITIZED effective configuration, never credentials.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '30s';
WITH cfg AS (SELECT :'config'::jsonb AS c),
mappings AS (
 SELECT 'SnappTrip'::text provider, "ProviderCityId" native_city, "CityId" city_id FROM hotels.snapptrip_city_mappings
 UNION ALL
 SELECT 'Lamasoo', "ProviderCityId", "CityId" FROM hotels.lamaso_city_mappings WHERE "IsEnabled"
), states AS (
 SELECT 'SnappTrip'::text provider, "NativeCityId" native_city, "LastSuccessAtUtc" success, "Status" status FROM hotels.snapptrip_discovery_sync_states
 UNION ALL
 SELECT 'Lamasoo', "NativeCityId", "LastSuccessAtUtc", "Status" FROM hotels.lamaso_discovery_sync_states
), hotels AS (
 SELECT 'SnappTrip'::text provider, "NativeCityId" native_city, "Published" published, "ObservedAtUtc" observed,
 "ContentReady" content_ready, "Latitude" lat, "Longitude" lon, "ImagesJson" images FROM hotels.snapptrip_discovery_hotels
 UNION ALL
 SELECT 'Lamasoo', "NativeCityId", "Published", "ObservedAtUtc", "ContentReady", "Latitude", "Longitude", "ImagesJson" FROM hotels.lamaso_discovery_hotels
), expected AS (
 SELECT m.*, city."Id" IS NOT NULL AND province."Id" IS NOT NULL valid_mapping,
 (cfg.c->'local_scopes') ? (m.provider || ':' || m.city_id) local_content
 FROM mappings m CROSS JOIN cfg
 LEFT JOIN "references".cities city ON city."Id"=m.city_id
 LEFT JOIN "references".provinces province ON province."Id"=city."ProvinceId"
 WHERE (cfg.c->'sources') ? 'hotels' AND (cfg.c->'providers') ? m.provider
 AND (city."Id" IS NULL OR province."Id" IS NULL OR city."IsActive" AND province."IsActive")
), hotel_counts AS (
 SELECT e.provider, e.native_city, e.city_id, e.valid_mapping, e.local_content, s.success, s.status,
 count(h.native_city) FILTER (WHERE h.published) published,
 count(h.native_city) FILTER (WHERE h.published AND NOT e.local_content AND h.observed < CURRENT_TIMESTAMP-make_interval(days=>(cfg.c->>'hotel_max_age_days')::int)) expired,
 count(h.native_city) FILTER (WHERE h.published AND e.local_content AND h.content_ready IS DISTINCT FROM true) pending_details,
 count(h.native_city) FILTER (WHERE h.published AND (h.lat IS NULL OR h.lon IS NULL)) unknown_location,
 count(h.native_city) FILTER (WHERE h.published AND jsonb_array_length(h.images::jsonb)=0) missing_images,
 (cfg.c->>'hotel_max_age_days')::int max_age
 FROM expected e CROSS JOIN cfg LEFT JOIN states s USING(provider,native_city)
 LEFT JOIN hotels h USING(provider,native_city)
 GROUP BY e.provider,e.native_city,e.city_id,e.valid_mapping,e.local_content,s.success,s.status,cfg.c
), hotel_report AS (
 SELECT jsonb_build_object('source','hotels','provider',provider,'scope',native_city,'cityId',city_id,
 'expectedMappings',1,'validMappings',valid_mapping::int,'lastSuccessUtc',success,'status',status,
 'published',published,'expired',expired,'pendingDetails',pending_details,'unknownLocation',unknown_location,'missingImages',missing_images,
 'reasons',to_jsonb(array_remove(ARRAY[
 CASE WHEN NOT valid_mapping THEN 'invalid_mapping' END,
 CASE WHEN success IS NULL THEN 'missing_sync' END,
 CASE WHEN success IS NOT NULL AND status IS DISTINCT FROM 'ready' THEN 'sync_failed' END,
 CASE WHEN NOT local_content AND success < CURRENT_TIMESTAMP-make_interval(days=>max_age) OR expired>0 THEN 'expired_data' END,
 CASE WHEN pending_details>0 THEN 'pending_details' END],NULL))) row
 FROM hotel_counts
), entertainment_mappings AS (
 SELECT m.*, city."Id" IS NOT NULL AND city."IsActive" AND province."IsActive" valid_mapping
 FROM entertainments.touristpanel_location_mappings m CROSS JOIN cfg
 LEFT JOIN "references".cities city ON city."Id"=m."CityId"
 LEFT JOIN "references".provinces province ON province."Id"=city."ProvinceId"
 WHERE m."AccountKey"=cfg.c->>'account_key'
), entertainment_counts AS (
 SELECT s."LastSuccessUtc" success,s."Status" status,
 (SELECT count(*) FROM entertainment_mappings) expected_mappings,
 (SELECT count(*) FROM entertainment_mappings WHERE valid_mapping) valid_mappings,
 count(p."ProgramKey") published,
 count(p."ProgramKey") FILTER (WHERE p."ObservedAtUtc" IS NULL OR btrim(p."Title")='') pending_details,
 count(p."ProgramKey") FILTER (WHERE p."ObservedAtUtc"<CURRENT_TIMESTAMP-make_interval(days=>(cfg.c->>'entertainment_max_age_days')::int)
 OR p."ListedAtUtc"<CURRENT_TIMESTAMP-make_interval(days=>(cfg.c->>'entertainment_max_age_days')::int)) expired,
 count(p."ProgramKey") FILTER (WHERE NOT EXISTS(SELECT 1 FROM entertainment_mappings m WHERE m."LocationCode"=p."LocationCode" AND m.valid_mapping)) unknown_location,
 count(p."ProgramKey") FILTER (WHERE cardinality(p."Images")=0) missing_images,
 (cfg.c->>'entertainment_max_age_days')::int max_age
 FROM cfg LEFT JOIN entertainments.touristpanel_sync_states s ON s."AccountKey"=cfg.c->>'account_key'
 LEFT JOIN entertainments.touristpanel_programs p ON p."AccountKey"=cfg.c->>'account_key' AND p."IsListed"
 WHERE (cfg.c->'sources') ? 'entertainments'
 GROUP BY s."LastSuccessUtc",s."Status",cfg.c
), entertainment_report AS (
 SELECT jsonb_build_object('source','entertainments','provider','TouristPanel','scope','current-account',
 'expectedMappings',expected_mappings,'validMappings',valid_mappings,'lastSuccessUtc',success,'status',status,
 'published',published,'expired',expired,'pendingDetails',pending_details,'unknownLocation',unknown_location,'missingImages',missing_images,
 'reasons',to_jsonb(array_remove(ARRAY[
 CASE WHEN success IS NULL THEN 'missing_sync' END,
 CASE WHEN success IS NOT NULL AND status IS DISTINCT FROM 'Ready' THEN 'sync_failed' END,
 CASE WHEN success<CURRENT_TIMESTAMP-make_interval(days=>max_age) OR expired>0 THEN 'expired_data' END,
 CASE WHEN pending_details>0 THEN 'pending_details' END,
 CASE WHEN expected_mappings<>valid_mappings THEN 'invalid_mapping' END],NULL))) row
 FROM entertainment_counts
), reports AS (SELECT row FROM hotel_report UNION ALL SELECT row FROM entertainment_report)
SELECT jsonb_build_object('observedAtUtc',CURRENT_TIMESTAMP,'scopes',coalesce(jsonb_agg(row ORDER BY row->>'source',row->>'provider',row->>'scope'),'[]')) FROM reports;
ROLLBACK;
