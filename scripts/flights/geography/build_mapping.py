"""Build reviewed, replayable SQL from a geography snapshot and pinned public evidence. No network/DB writes."""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
from prepare_evidence import load, normalize
from snapshot import write_json

ROOT = Path(__file__).resolve().parent
VERSION = 'flight-geography-20261020'


def distance(a, b):
    lat1, lon1, lat2, lon2 = map(math.radians, [float(a['latitude_deg']), float(a['longitude_deg']),
        b['latitude'], b['longitude']])
    h = math.sin((lat2-lat1)/2)**2 + math.cos(lat1)*math.cos(lat2)*math.sin((lon2-lon1)/2)**2
    return 6371 * 2 * math.asin(min(1, math.sqrt(h)))


def choose_actual(airport, evidence, index):
    sources = [a for a in evidence if a['iata_code'] == airport['iata_code'] and a['type'] != 'closed']
    if len(sources) != 1: return None, 'missing_or_ambiguous_airport_evidence', sources
    source = sources[0]
    if airport.get('icao_code') and source.get('icao_code') and airport['icao_code'] != source['icao_code']:
        return None, 'airport_icao_identity_conflict', sources
    if airport.get('latitude') is not None and airport.get('longitude') is not None:
        if distance(source, dict(latitude=float(airport['latitude']), longitude=float(airport['longitude']))) > 20:
            return None, 'airport_position_identity_conflict', sources
    elif airport['country_code'] != source['iso_country']:
        return None, 'airport_country_identity_conflict', sources
    candidates = index.get((source['iso_country'], normalize(source['municipality'])), [])
    # Exact municipality identity plus location corroboration; never nearest-city inference.
    candidates = [c for c in candidates if distance(source, c) <= 80]
    if len(candidates) != 1: return None, 'missing_or_ambiguous_municipality', sources
    return candidates[0], None, sources


def choose_commercial(city, index):
    groups = [{g['id']: g for g in index.get((city['country_code'], normalize(city[key])), [])}
        for key in ['city_name_en', 'city_name_fa']]
    populated = [g for g in groups if g]
    if not populated: return None
    ids = set.intersection(*(set(g) for g in populated))
    if not ids: return None
    candidates = {k:v for group in populated for k,v in group.items() if k in ids}
    # Independent bilingual agreement first; primary names disambiguate broad alternate-name aliases.
    direct = [g for g in candidates.values() if
        {normalize(g['name']), normalize(g['ascii'])} & {normalize(city['city_name_en']),normalize(city['city_name_fa'])}]
    if len(direct)==1: return direct[0]
    return next(iter(candidates.values())) if len(candidates)==1 else None


def build(snapshot, evidence, date, version=VERSION):
    provinces = {p['Id']: p for p in snapshot['provinces']}
    countries = {c['Code']: c for c in snapshot['countries']}
    country_source = {c['code']: c for c in evidence['countries']}
    refs = list(snapshot['cities'])
    reference_names = collections.defaultdict(dict)
    for c in refs:
        for key in {normalize(c['Name']),normalize(c['NameEn'])}:
            reference_names[c['ProvinceId'],key][c['Id']] = c
    airport_sources = collections.defaultdict(list)
    for a in evidence['airports']: airport_sources[a['iata_code']].append(a)
    geo_index = collections.defaultdict(list)
    for c in evidence['cities']:
        for key in {normalize(x) for x in [c['name'], c['ascii'], *c['aliases']]}:
            geo_index[c['country'], key].append(c)
    next_city = max(c['Id'] for c in refs) + 1
    next_province = max(provinces) + 1
    additions = dict(countries=[], provinces=[], cities=[])
    airport_mappings, commercial_mappings, review, conflicts = [], [], [], []
    allocated = {}
    allocated_cities = {}

    def reference(geo):
        nonlocal next_city, next_province
        if geo['id'] in allocated: return allocated[geo['id']], None
        country = geo['country']
        ps = [p for p in provinces.values() if p['CountryCode'] == country and (
            p.get('GeoNamesAdmin1Code') == country+'.'+geo['admin1'] if country == 'IR' else p.get('IsSynthetic'))]
        if len(ps) > 1: return None, 'ambiguous_reference_province'
        if not ps:
            if country == 'IR': return None, 'unknown_iran_province'
            if country not in country_source: return None, 'unknown_country'
            if country not in countries:
                co = country_source[country]
                countries[country] = dict(Code=country, Name=co['name'], NameEn=co['name'], IsActive=True)
                additions['countries'].append(countries[country])
            p = dict(Id=next_province, Name=countries[country]['Name'], NameEn=countries[country]['NameEn'],
                Slug='flight-country-'+country.lower(), SortOrder=next_province, IsActive=True,
                CountryCode=country, GeoNamesAdmin1Code=None, IsSynthetic=True)
            next_province += 1
            provinces[p['Id']] = p
            additions['provinces'].append(p)
        else: p = ps[0]
        names = {normalize(x) for x in [geo['name'], geo['ascii'], *geo['aliases']]}
        matches = list({k:c for name in names for k,c in reference_names[p['Id'],name].items()}.values())
        if len(matches) > 1: return None, 'ambiguous_reference_city'
        if matches:
            c = matches[0]
            # Two distinct GeoNames identities cannot silently collapse into one synthetic-country city.
            if c['Id'] in allocated_cities and allocated_cities[c['Id']] != geo['id']:
                return None, 'homonymous_geonames_city'
            if not c['IsActive'] or not p['IsActive'] or not countries[country]['IsActive']:
                return None, 'inactive_reference'
        else:
            c = dict(Id=next_city, Name=geo['name'], NameEn=geo['ascii'] or geo['name'],
                Slug='geonames-'+str(geo['id']), ProvinceId=p['Id'], SortOrder=next_city,
                IsActive=True, LocationKind='city')
            next_city += 1
            refs.append(c)
            for key in {normalize(c['Name']),normalize(c['NameEn'])}:
                reference_names[c['ProvinceId'],key][c['Id']] = c
            additions['cities'].append(c)
        allocated[geo['id']] = c
        allocated_cities[c['Id']] = geo['id']
        return c, None

    def mapping(key, source, geo, ref, method, airport_evidence=None):
        return dict(code=key, city_id=ref['Id'], source_identity=json.dumps(source, ensure_ascii=False, sort_keys=True),
            method=method, evidence=dict(geonames=geo, airport=airport_evidence, sources=evidence['sources']),
            source_version=version, snapshot_date=date)

    for airport in sorted(snapshot['airports'], key=lambda a: a['iata_code']):
        geo, reason, sources = choose_actual(airport, airport_sources[airport['iata_code']], geo_index)
        ref = None
        if geo: ref, reason = reference(geo)
        if ref:
            airport_mappings.append(mapping(airport['iata_code'], airport, geo, ref,
                'ourairports_municipality_geonames_exact_and_distance', sources[0]))
            if geo['country'] != airport['country_code'] or normalize(airport['city_name_en']) not in {
                normalize(x) for x in [geo['name'], geo['ascii'], *geo['aliases']]}:
                conflicts.append(dict(airport=airport['iata_code'], raw_country=airport['country_code'],
                    raw_city=airport['city_name_en'], actual_country=geo['country'], actual_city=geo['name']))
        else:
            review.append(dict(kind='airport', code=airport['iata_code'], reason=reason,
                country=airport['country_code'], name=airport['city_name_en'], is_active=airport['is_active'],
                evidence_airport_ids=[s['id'] for s in sources],
                candidate_geonames_ids=sorted({g['id'] for s in sources
                    for g in geo_index.get((s['iso_country'],normalize(s['municipality'])),[])})))
    for city in sorted(snapshot['search_cities'], key=lambda c: c['city_code']):
        geo = choose_commercial(city,geo_index)
        if geo:
            ref, reason = reference(geo)
        else: ref, reason = None, 'missing_or_ambiguous_commercial_city'
        if ref:
            commercial_mappings.append(mapping(city['city_code'], city, geo, ref, 'commercial_city_exact_country_geonames'))
        else:
            review.append(dict(kind='commercial', code=city['city_code'], reason=reason,
                country=city['country_code'], name=city['city_name_en'],
                candidate_geonames_ids=sorted({g['id'] for key in ['city_name_en','city_name_fa']
                    for g in geo_index.get((city['country_code'],normalize(city[key])),[])})))
    return dict(version=version, date=date, additions=additions, airports=airport_mappings,
        commercial=commercial_mappings, remaining=review, conflicts=conflicts)


def sql_literal(value):
    if isinstance(value, (dict, list)): value = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    return "'"+str(value).replace("'", "''")+"'"


def make_sql(result, snapshot, template=None):
    payload = {k: result[k] for k in ['version', 'date', 'additions', 'airports', 'commercial']}
    # Exact public identities guard replay and deployment onto a different geography/catalogue.
    payload['expected_airports'] = snapshot['airports']
    payload['expected_commercial'] = snapshot['search_cities']
    payload['expected_cities'] = snapshot['cities']
    payload['expected_provinces'] = snapshot['provinces']
    payload['expected_countries'] = snapshot['countries']
    payload['expected_memberships'] = snapshot['search_memberships']
    payload['digest'] = hashlib.sha256(json.dumps(payload, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
    header = "BEGIN;\nSET LOCAL client_encoding='UTF8';\nSET LOCAL standard_conforming_strings=on;\nSET LOCAL lock_timeout='15s';\nSELECT pg_advisory_xact_lock(174593,20261019);\n"
    body = (template or ROOT/'import-template.sql').read_text(encoding='utf-8')
    return header + body.replace('__PAYLOAD__', sql_literal(payload)) + '\nCOMMIT;\n'


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--snapshot', type=Path, default=ROOT/'2026-10-20/inputs/geography.json.gz')
    p.add_argument('--evidence', type=Path, default=ROOT/'2026-10-20/inputs/evidence.json.gz')
    p.add_argument('--date', default='2026-10-20')
    p.add_argument('--version', default=VERSION)
    p.add_argument('--template', type=Path, default=ROOT/'import-template.sql')
    p.add_argument('--output', type=Path, default=ROOT/'2026-10-20')
    a = p.parse_args()
    snapshot, evidence = load(a.snapshot), load(a.evidence)
    result = build(snapshot, evidence, a.date, a.version)
    result['source_hashes'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [a.snapshot, a.evidence]}
    result['coverage'] = dict(total_airports=len(snapshot['airports']), mapped_airports=len(result['airports']),
        total_commercial=len(snapshot['search_cities']), mapped_commercial=len(result['commercial']),
        by_country=dict(collections.Counter(r['country'] for r in result['remaining'] if r['kind']=='airport')),
        selectable_airports=len({r['airport_code'] for r in snapshot['search_memberships']}),
        mapped_selectable_airports=len({r['airport_code'] for r in snapshot['search_memberships']} & {r['code'] for r in result['airports']}))
    mapped_codes = {r['code'] for r in result['airports']}
    country_totals = collections.Counter(r['country_code'] for r in snapshot['airports'])
    country_mapped = collections.Counter(r['country_code'] for r in snapshot['airports'] if r['iata_code'] in mapped_codes)
    result['coverage']['countries'] = {code:dict(total=total,mapped=country_mapped[code],remaining=total-country_mapped[code])
        for code,total in sorted(country_totals.items())}
    a.output.mkdir(parents=True, exist_ok=True)
    (a.output/'02-data.sql').write_text(make_sql(result, snapshot, a.template), encoding='utf-8', newline='\n')
    write_json(a.output/'mapping-report.json.gz', result)
    write_json(a.output/'remaining.json', result['remaining'])
    write_json(a.output/'conflicts.json', result['conflicts'])
    write_json(a.output/'validation.json', dict(coverage=result['coverage'], additions={k:len(v) for k,v in result['additions'].items()},
        source_hashes=result['source_hashes'], sql_sha256=hashlib.sha256((a.output/'02-data.sql').read_bytes()).hexdigest()))
    print(json.dumps(result['coverage'], ensure_ascii=False))


if __name__ == '__main__': main()
