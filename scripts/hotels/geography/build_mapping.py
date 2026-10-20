"""Archived V1 generator and SQL-literal helpers. Use build_mapping_v2.py for deployment."""
import argparse
import collections
import csv
import hashlib
import json
import re
import unicodedata
from pathlib import Path


def normalize(value):
    value = unicodedata.normalize('NFKC', value or '').translate(str.maketrans('يك', 'یک'))
    return ''.join(c for c in value if c.isalnum()).casefold()


def read_reference_rows(text, table):
    rows = []
    pattern = r'INSERT INTO "references"\.' + table + r' \(([^\n]+?)\) VALUES \(([^\n]+?)\);'
    token = r"'(?:''|[^'])*'|\btrue\b|\bfalse\b|\bNULL\b|\d+"
    for match in re.finditer(pattern, text):
        columns = re.findall(r'"([^"]+)"', match[1])
        values = re.findall(token, match[2])
        assert re.sub(token, '', match[2]).replace(',', '').strip() == '', 'Unsupported SQL literal'
        assert len(columns) == len(values)
        def parse(v):
            if v.startswith("'"): return v[1:-1].replace("''", "'")
            return {'true': True, 'false': False, 'NULL': None}.get(v, int(v) if v.isdigit() else v)
        rows.append(dict(zip(columns, map(parse, values))))
    assert rows and len({x['Id'] for x in rows}) == len(rows)
    return rows


def build(dump, snapshot, output):
    output.mkdir(parents=True, exist_ok=True)
    source = dump.read_text(encoding='utf-8-sig')
    cities, provinces = read_reference_rows(source, 'cities'), read_reference_rows(source, 'provinces')
    province_by_name = {normalize(p['Name']): p for p in provinces}
    province_by_id = {p['Id']: p for p in provinces}
    city_by_name = collections.defaultdict(list)
    for city in cities:
        assert city['ProvinceId'] in province_by_id
        if city['IsActive']: city_by_name[normalize(city['Name'])].append(city)
    def load(name): return json.loads((snapshot / name).read_text(encoding='utf-8-sig'))
    snapp = load('snapptrip-cities.raw.json')
    lamas = load('lamasoo-cities.raw.json')['cities']
    hotels = load('lamasoo-inventory.raw.json')['hotels']
    observations = collections.defaultdict(list)
    for hotel in hotels: observations[hotel.get('city')].append(hotel)
    assert len({c['id'] for c in snapp}) == len(snapp)
    assert len({c['id'] for c in lamas}) == len(lamas)
    opaque = lambda value: bool(re.fullmatch('[0-9a-f]{24}', value or ''))
    iran_literals = {normalize(v) for v in ['ایران', 'Iran', 'IR', 'IRN']}
    country_support = collections.defaultdict(set)
    country_conflicts = set()
    for city_id, items in observations.items():
        literal = {normalize(h.get('country')) for h in items if h.get('country') and not opaque(h['country'])}
        literal.discard('')
        for h in items:
            if opaque(h.get('country')):
                if literal & iran_literals: country_support[h['country']].add(city_id)
                if literal - iran_literals: country_conflicts.add(h['country'])
    iran_ids = {key for key, support in country_support.items() if len(support) >= 3 and key not in country_conflicts}
    def is_iran(country): return normalize(country) in iran_literals or country in iran_ids
    records, province_records = [], []
    def record(provider, external, name, code, candidates, matched, method, evidence, reason):
        return dict(provider=provider, external_id=str(external), name=name, code=code,
                    reference_id=matched['Id'] if matched else None,
                    reference_name=matched['Name'] if matched else None,
                    province_id=matched['ProvinceId'] if matched else None,
                    candidate_ids=[c['Id'] for c in candidates], status='mapped' if matched else 'needs_review',
                    method=method if matched else None, reason=reason, evidence=evidence)
    for city in snapp:
        state = city['state']
        province = province_by_name.get(normalize(state['title']))
        candidates = city_by_name[normalize(city['title_fa'])]
        scoped = [c for c in candidates if province and c['ProvinceId'] == province['Id']]
        matched = scoped[0] if len(scoped) == 1 else None
        records.append(record('SnappTrip', city['id'], city['title_fa'], None, scoped or candidates, matched,
                              'exact_city_and_province', {'state': state},
                              None if matched else 'province_outside_reference' if not province else
                              'ambiguous_reference_city' if len(scoped) > 1 else 'city_not_found_in_province'))
    snapp_states = {}
    for city in snapp:
        state = city['state']
        snapp_states.setdefault((state['id'],state['title']), dict(state, city_ids=[]))['city_ids'].append(city['id'])
    for state in snapp_states.values():
        matched = province_by_name.get(normalize(state['title']))
        province_records.append(dict(provider='SnappTrip', external_key=str(state['id']), key_kind='native_id',
                                     source_country=None, source_name=state['title'], reference_id=matched['Id'] if matched else None,
                                     method='exact_province_name' if matched else None,
                                     evidence={'city_ids': state['city_ids']}, reason=None if matched else 'outside_reference'))

    # Native province IDs are accepted only when every independently identified city agrees.
    inferred_provinces = {}
    def match_lamas(city, province_ids):
        items = observations[city['id']]
        candidates = city_by_name[normalize(city['name'])]
        countries = {h.get('country') for h in items if normalize(h.get('country'))}
        recognized = set()
        for h in items:
            value = h.get('province')
            p = province_by_name.get(normalize(value))
            if p: recognized.add(p['Id'])
            elif value in province_ids: recognized.add(province_ids[value])
        has_iran = any(is_iran(country) for country in countries)
        has_foreign = any(not is_iran(country) for country in countries)
        scoped = [c for c in candidates if not recognized or c['ProvinceId'] in recognized]
        evidence = {'inventory': [{'hotel_id': h['id'], 'province': h.get('province'), 'country': h.get('country')} for h in items]}
        matched = None
        if has_foreign: reason = 'unresolved_or_foreign_country'
        elif not has_iran: reason = 'missing_country_evidence'
        elif len(recognized) > 1: reason = 'conflicting_provider_provinces'
        elif not scoped: reason = 'city_or_province_mismatch'
        elif len(scoped) != 1: reason = 'ambiguous_reference_city'
        else: matched, reason = scoped[0], None
        return record('Lamasoo', city['id'], city['name'], city.get('code'), scoped or candidates, matched,
                      'exact_city_country_and_province' if recognized else 'exact_unique_city_and_country', evidence, reason)

    initial = {c['id']: match_lamas(c, {}) for c in lamas}
    province_support = collections.defaultdict(set)
    blocked_provinces = set()
    for hotel in hotels:
        value = hotel.get('province')
        if not opaque(value): continue
        r = initial.get(hotel.get('city'))
        if not is_iran(hotel.get('country')) or not r or not r['reference_id']:
            blocked_provinces.add(value)
        else: province_support[value].add(r['province_id'])
    for value, support in province_support.items():
        if len(support) == 1 and value not in blocked_provinces:
            inferred_provinces[value] = next(iter(support))
    lamas_records = [match_lamas(c, inferred_provinces) for c in lamas]
    # A duplicate native city can reuse independently established geography only with the same exact code AND name.
    established = collections.defaultdict(set)
    for r in lamas_records:
        if r['reference_id'] and r['code']: established[(normalize(r['name']), r['code'])].add(r['reference_id'])
    by_id = {c['Id']: c for c in cities}
    for r in lamas_records:
        siblings = established[(normalize(r['name']), r['code'])]
        if r['reason'] == 'missing_country_evidence' and len(siblings) == 1:
            matched = by_id[next(iter(siblings))]
            r.update(reference_id=matched['Id'], reference_name=matched['Name'], province_id=matched['ProvinceId'],
                     method='same_name_and_code_as_geographically_verified_city', reason=None, status='mapped')
            r['evidence']['verified_sibling_ids'] = [x['external_id'] for x in lamas_records if x['reference_id'] == matched['Id']
                                                   and x['code'] == r['code'] and x['external_id'] != r['external_id']
                                                   and x['method'] != r['method']]
    records.extend(lamas_records)
    geo = collections.defaultdict(list)
    for h in hotels:
        if h.get('province'): geo[(h['province'], h.get('country'))].append(h)
    for (value, country), items in geo.items():
        p = province_by_name.get(normalize(value))
        target = p['Id'] if p else inferred_provinces.get(value)
        matched = target if is_iran(country) else None
        province_records.append(dict(provider='Lamasoo', external_key=value, key_kind='native_id' if opaque(value) else 'raw_value',
                                     source_country=country, source_name=None if opaque(value) else value, reference_id=matched,
                                     method=('consistent_verified_city_observations' if opaque(value) else 'exact_province_and_country') if matched else None,
                                     evidence={'hotel_ids': [h['id'] for h in items], 'city_ids': sorted({h['city'] for h in items if h.get('city')})},
                                     reason=None if matched else 'conflicting_or_insufficient_province_evidence'))
    mapped = [r for r in records if r['reference_id']]
    for r in records:
        r['candidates'] = [dict(id=c['Id'], name=c['Name'], province_id=c['ProvinceId'],
                                province_name=province_by_id[c['ProvinceId']]['Name'])
                           for c in cities if c['Id'] in r['candidate_ids']]
    province_mappings = {}
    for r in province_records:
        if r['reference_id']:
            key = (r['provider'], r['key_kind'], r['external_key'])
            if key in province_mappings:
                assert province_mappings[key]['reference_id'] == r['reference_id']
                province_mappings[key]['evidence']['additional_observations'] = province_mappings[key]['evidence'].get('additional_observations', []) + [r['evidence']]
            else: province_mappings[key] = dict(r, evidence=dict(r['evidence']))
    for r in mapped:
        assert r['reference_id'] in r['candidate_ids']
        assert r['province_id'] == by_id[r['reference_id']]['ProvinceId']
        assert r['method'] and not r['reason']
    assert len({(r['provider'], r['external_id']) for r in records}) == len(records)
    duplicate_cities = collections.defaultdict(list)
    for c in cities: duplicate_cities[(c['ProvinceId'], normalize(c['Name']))].append(c)
    report = {'reference': {'cities': len(cities), 'provinces': len(provinces),
                            'duplicate_city_groups': [group for group in duplicate_cities.values() if len(group) > 1]},
              'summary': {p: dict(collections.Counter(r['status'] for r in records if r['provider'] == p)) for p in ['SnappTrip', 'Lamasoo']},
              'province_mapping_count': len(province_mappings), 'iran_native_country_evidence': {key: sorted(country_support[key]) for key in sorted(iran_ids)},
              'sources': [{'file': str(f), 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()} for f in [dump] + [snapshot / n for n in ['snapptrip-cities.raw.json','lamasoo-cities.raw.json','lamasoo-inventory.raw.json']]],
              'cities': records, 'provinces': province_records}
    (output / 'mapping-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    with (output / 'cities-review.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        fields = ['provider','external_id','name','code','reason','candidate_ids']
        writer = csv.DictWriter(stream, fields); writer.writeheader()
        for r in records:
            if not r['reference_id']: writer.writerow({k:r[k] for k in fields})
    with (output / 'provinces-review.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        fields = ['provider','external_key','key_kind','source_country','source_name','reason']
        writer = csv.DictWriter(stream, fields); writer.writeheader()
        for r in province_records:
            if not r['reference_id']: writer.writerow({k:r[k] for k in fields})
    def literal(v):
        if v is None: return 'NULL'
        if isinstance(v, int): return str(v)
        if isinstance(v, (dict,list)): v=json.dumps(v, ensure_ascii=False, separators=(',',':'))
        return "'" + str(v).replace("'", "''") + "'"
    def values(rows): return ',\n'.join('(' + ', '.join(map(literal, row)) + ')' for row in rows) + ';'
    template = Path(__file__).with_name('mapping-template.sql').read_text(encoding='utf-8')
    replacements = {
        '@@EXPECTED_CITIES@@': values((c['Id'],c['Name'],c['ProvinceId']) for c in cities if c['Id'] in {r['reference_id'] for r in mapped}),
        '@@EXPECTED_PROVINCES@@': values((p['Id'],p['Name']) for p in provinces),
        '@@CITY_MAPPINGS@@': values((r['provider'],r['external_id'],r['reference_id'],r['name'],r['code'],r['method'],r['evidence']) for r in mapped),
        '@@PROVINCE_MAPPINGS@@': values((r['provider'],r['external_key'],r['key_kind'],r['reference_id'],r['source_name'],r['method'],r['evidence']) for r in province_mappings.values()),
    }
    for key,value in replacements.items(): template = template.replace(key,value)
    assert '@@' not in template
    (output / '2026-10-19-provider-geography.sql').write_text(template, encoding='utf-8')
    print(json.dumps({k:report[k] for k in ['reference','summary','province_mapping_count']}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.error('V1 is archived; use build_mapping_v2.py with the country gazetteer snapshot.')
    parser.add_argument('--dump', type=Path, required=True)
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path(__file__).parent / '2026-10-19')
    args = parser.parse_args()
    build(args.dump, args.snapshot, args.output)
