"""Offline reference additions/deduplication and module-owned provider mappings."""
import argparse
import collections
import csv
import hashlib
import json
import re
import zipfile
from pathlib import Path
from urllib.parse import urlparse
from build_mapping import normalize, read_reference_rows


def deduplicate(cities):
    groups = collections.defaultdict(list)
    for city in cities: groups[(city['ProvinceId'], normalize(city['Name']))].append(city)
    kept, merges = [], []
    for group in groups.values():
        group.sort(key=lambda c: c['Id'])
        kept.append(dict(group[0]))
        merges.extend(dict(old_id=c['Id'], new_id=group[0]['Id'], before=c) for c in group[1:])
    return sorted(kept, key=lambda c: c['Id']), merges


class Gazetteer:
    def __init__(self, directory, wanted):
        self.by_name = collections.defaultdict(dict)
        self.admin = {}
        for line in (directory/'admin1CodesASCII.txt').read_text(encoding='utf-8').splitlines():
            f=line.split('\t'); self.admin[f[0]]=dict(key=f[0], name=f[1], name_en=f[2], geonames_id=f[3])
        self.countries={}
        for line in (directory/'countryInfo.txt').read_text(encoding='utf-8').splitlines():
            if line and not line.startswith('#'):
                f=line.split('\t'); self.countries[f[0]]=dict(code=f[0], name=f[4])
        wanted.discard('')
        for archive,member in [('cities500.zip','cities500.txt'),('IQ.zip','IQ.txt')]:
            with zipfile.ZipFile(directory/archive) as z:
                for line in z.read(member).decode('utf-8').splitlines():
                    f=line.split('\t')
                    if f[6]!='P':continue
                    names={normalize(n) for n in [f[1],f[2]]+f[3].split(',')}
                    matched=names & wanted
                    if matched:
                        row=dict(id=int(f[0]),name=f[1],name_en=f[2],country=f[8],admin=f[8]+'.'+f[10],feature=f[7])
                        for name in matched: self.by_name[name][row['id']]=row

    def find(self, name, english=None, country=None, province_key=None):
        def eligible(v):return (not country or v['country']==country) and (not province_key or v['admin']==province_key)
        rows={k:v for k,v in self.by_name.get(normalize(name), {}).items() if eligible(v)}
        if english:
            en={k:v for k,v in self.by_name.get(normalize(english), {}).items() if eligible(v)}
            rows={k:v for k,v in rows.items() if k in en} if rows else dict(en)
        return list(rows.values())


def validate_decision(decision, record, admin):
    if record['reference_id'] is not None:
        raise ValueError('Research cannot replace an existing mapping')
    if (decision['expected_name'],decision['expected_code'])!=(record['name'],record['code']):
        raise ValueError('Research snapshot name/code changed')
    geo=decision['geonames']; key=decision['admin1']
    if key not in admin or key.split('.')[0]!=decision['country'] or geo['admin1']!=key or geo['country']!=decision['country']:
        raise ValueError('Research country/admin1 mismatch')
    urls={e.get('url') or e.get('source_url') for e in decision['evidence']}
    urls.discard(None)
    domains={urlparse(url).netloc.lower().removeprefix('www.') for url in urls if urlparse(url).scheme=='https'}
    if len(domains)<2 or not re.fullmatch('[a-f0-9]{64}',geo.get('source_sha256','')):
        raise ValueError('Research requires recorded independent evidence')
    if decision['location_kind'] not in {'city','destination'}:
        raise ValueError('Unsupported location kind')
    if decision.get('allow_inventory_province_conflict') and not (
        record['external_id']=='5ae5ab74734a9af40a7ab325' and geo['id']==142676
        and decision['method']=='official_hotel_identity_and_address'):
        raise ValueError('Inventory exception requires a separately reviewed hotel identity')


def build(args):
    out=args.output; out.mkdir(parents=True,exist_ok=True)
    if getattr(args,'decisions',None) and out.resolve()==(Path(__file__).parent/'2026-10-19-v2').resolve():
        raise ValueError('Write reviewed results to a new output directory; V2 is immutable')
    source=args.dump.read_text(encoding='utf-8-sig')
    original=read_reference_rows(source,'cities'); provinces=read_reference_rows(source,'provinces')
    cities,merges=deduplicate(original)
    next_city=max(c['Id'] for c in original)+1
    next_province=max(p['Id'] for p in provinces)+1
    load=lambda n:json.loads((args.snapshot/n).read_text(encoding='utf-8-sig'))
    snapp=load('snapptrip-cities.raw.json'); lamas=load('lamasoo-cities.raw.json')['cities']; hotels=load('lamasoo-inventory.raw.json')['hotels']
    wanted={normalize(c['Name']) for c in cities}|{normalize(c['title_fa']) for c in snapp}|{normalize(c.get('title_en')) for c in snapp}|{normalize(c['name']) for c in lamas}
    gaz=Gazetteer(args.gazetteer,wanted)
    province_names={normalize(p['Name']):p for p in provinces}
    province_keys={}
    for p in provinces:
        matches=[a for a in gaz.admin.values() if a['key'].startswith('IR.') and normalize(a['name_en'].removesuffix(' Province'))==normalize(p['NameEn'])]
        assert len(matches)==1,(p['Name'],matches)
        p.update(CountryCode='IR',GeoNamesAdmin1Code=matches[0]['key'])
        province_keys[p['GeoNamesAdmin1Code']]=p
    by_geo={}; additions=[]; records=[]; obs=collections.defaultdict(list)
    for h in hotels: obs[h.get('city')].append(h)
    for c in cities:
        p=next(p for p in provinces if p['Id']==c['ProvinceId'])
        found=gaz.find(c['Name'],country='IR',province_key=p['GeoNamesAdmin1Code'])
        if len(found)==1: by_geo.setdefault(found[0]['id'],[]).append(c)
    # Existing canonical cities are matched by province-qualified names; aliases never delete another city.
    countries={'IR':dict(code='IR',name='ایران',name_en='Iran')}
    catalog=[]
    for f in sorted(args.flight_catalog.glob('airports-*.json')):
        catalog.extend(json.loads(f.read_text(encoding='utf-8-sig'))['cities'])
    def country_hint(name,en=None):
        values={c['countryCode'] for c in catalog if normalize(name) in {normalize(c['cityFaName']),normalize(c['cityName'])}
                or en and normalize(en)==normalize(c['cityName'])}
        values.update(r['country'] for r in records if r['provider']=='SnappTrip' and r['reference_id'] and normalize(r['name'])==normalize(name))
        return next(iter(values)) if len(values)==1 else None
    country_id_support=collections.defaultdict(lambda:collections.defaultdict(set))
    for c in lamas:
        hint=country_hint(c['name'])
        if not hint:
            found=gaz.find(c['name'])
            if len(found)==1:hint=found[0]['country']
        if hint:
            for h in obs[c['id']]:
                value=h.get('country')
                if re.fullmatch('[a-f0-9]{24}',value or ''):country_id_support[value][hint].add(c['id'])
    native_countries={key:next(iter(values)) for key,values in country_id_support.items()
                      if len(values)==1 and len(next(iter(values.values())))>=2}
    iran_ids=set()
    supported=collections.defaultdict(set)
    for cid,items in obs.items():
        if any(normalize(h.get('country')) in {'ایران','iran','ir'} for h in items):
            for h in items:
                if re.fullmatch('[a-f0-9]{24}',h.get('country') or ''): supported[h['country']].add(cid)
    iran_ids={key for key,ids in supported.items() if len(ids)>=3}
    def explicit_country(items):
        codes=set()
        for h in items:
            v=h.get('country')
            if normalize(v) in {'ایران','iran','ir'} or v in iran_ids: codes.add('IR')
            elif v in native_countries: codes.add(native_countries[v])
        return next(iter(codes)) if len(codes)==1 else None
    def resolve(provider, external_id, name, code=None, english=None, known_province=None, country=None):
        nonlocal next_city,next_province
        proof={'inventory':[dict(hotel_id=h['id'],province=h.get('province'),country=h.get('country')) for h in obs[external_id]]} if provider=='Lamasoo' else {}
        r=dict(provider=provider,external_id=str(external_id),name=name,code=code,reference_id=None,reason=None,evidence=proof)
        records.append(r)
        if not normalize(name) or re.search(r'تست|غیرفعال|\btest\b',name,re.I): r['reason']='invalid_or_test_destination';return
        if normalize(name) in province_names and not any(normalize(c['Name'])==normalize(name) for c in cities):
            r['reason']='province_without_city_evidence';return
        hinted_country=country or country_hint(name,english)
        canonical=[c for c in cities if normalize(c['Name'])==normalize(name)]
        if provider=='Lamasoo' and not hinted_country:
            ir=gaz.find(name,country='IR')
            if len(canonical)==1:
                p=next(p for p in province_keys.values() if p['Id']==canonical[0]['ProvinceId'])
                qualified=gaz.find(name,country=p['CountryCode'],province_key=p['GeoNamesAdmin1Code'])
                if len(qualified)==1: hinted_country=p['CountryCode'];known_province=p
            if not hinted_country:
                global_matches=gaz.find(name)
                if len(global_matches)==1 and len(by_geo.get(global_matches[0]['id'],[]))==1:
                    target=by_geo[global_matches[0]['id']][0]
                    p=next(p for p in province_keys.values() if p['Id']==target['ProvinceId'])
                    hinted_country=p['CountryCode'];known_province=p
            if not hinted_country and len(ir)==1 and len(gaz.find(name))==1 and not canonical and not obs[external_id]:hinted_country='IR'
            if not hinted_country:r['reason']='missing_country_evidence';return
        if provider=='Lamasoo' and hinted_country and len(canonical)==1:
            p=next(p for p in province_keys.values() if p['Id']==canonical[0]['ProvinceId'])
            if p['CountryCode']==hinted_country:known_province=p
        found=gaz.find(name,english,hinted_country,known_province['GeoNamesAdmin1Code'] if known_province else None)
        if len(found)==1:
            geo=found[0]; country=geo['country']; key=geo['admin']
            if key not in gaz.admin: r['reason']='missing_admin1_reference';return
            if key not in province_keys:
                a=gaz.admin[key]
                province_keys[key]=dict(Id=next_province,Name=a['name'],NameEn=a['name_en'],Slug='geo-'+key.lower().replace('.','-'),
                                        CountryCode=country,GeoNamesAdmin1Code=key,SortOrder=next_province,IsActive=True)
                next_province+=1
            province=province_keys[key]
            proof['geonames']=geo
            exact=[c for c in cities if c['ProvinceId']==province['Id'] and normalize(c['Name'])==normalize(name)]
            aliases=by_geo.get(geo['id'],[])
            target=exact[0] if len(exact)==1 else aliases[0] if len(aliases)==1 else None
            if not target and len(aliases)>1: r['reason']='ambiguous_reference_alias';return
        else:
            province=known_province
            if not province and hinted_country=='IR':
                iran_provinces={p['Id']:p for p in province_keys.values() if p['CountryCode']=='IR'}
                candidates=[c for c in cities if normalize(c['Name'])==normalize(name) and c['ProvinceId'] in iran_provinces]
                if len(candidates)==1: province=iran_provinces[candidates[0]['ProvinceId']]
            if not province: r['reason']='ambiguous_geography' if found else 'missing_geography';r['geonames_candidates']=found;return
            country=province['CountryCode'];geo=None
            exact=[c for c in cities if c['ProvinceId']==province['Id'] and normalize(c['Name'])==normalize(name)]
            target=exact[0] if len(exact)==1 else None
        # A conflicting readable Inventory province must be reviewed, not silently ignored.
        reported={province_names[normalize(h.get('province'))]['Id'] for h in obs[external_id] if normalize(h.get('province')) in province_names} if provider=='Lamasoo' else set()
        if reported and reported!={province['Id']}: r['reason']='conflicting_inventory_province';return
        if not target:
            target=dict(Id=next_city,Name=name,NameEn=english or (geo['name_en'] if geo else ''),Slug='refahi-destination-'+str(next_city),
                        ProvinceId=province['Id'],SortOrder=next_city,IsActive=True,LocationKind='city' if geo and geo['feature']!='PPLX' else 'destination')
            cities.append(target);additions.append(target);next_city+=1
        if geo:
            entries=by_geo.setdefault(geo['id'],[])
            if all(c['Id']!=target['Id'] for c in entries):entries.append(target)
        countries.setdefault(country,dict(code=country,name=gaz.countries[country]['name'],name_en=gaz.countries[country]['name']))
        r.update(reference_id=target['Id'],province_id=province['Id'],country=country,method='exact_geonames_place_and_admin1' if geo else 'exact_provider_province' if provider=='SnappTrip' else 'exact_unique_name_country_and_reference_province',reason=None)
        proof['canonical_province']=dict(id=province['Id'],country=country,admin1=province['GeoNamesAdmin1Code'])
    for c in snapp:
        p=province_names.get(normalize(c['state']['title']))
        resolve('SnappTrip',c['id'],c['title_fa'],english=c.get('title_en') or None,known_province=p,country='IR' if p else None)
        records[-1]['evidence']['state']=c['state']
    for c in lamas: resolve('Lamasoo',c['id'],c['name'],c.get('code'),country=explicit_country(obs[c['id']]))
    research_path=getattr(args,'decisions',None)
    version=3 if research_path else 2
    if research_path:
        research=json.loads(research_path.read_text(encoding='utf-8'))
        baseline_path=Path(__file__).parent/'2026-10-19-v2/mapping-report.json'
        if hashlib.sha256(baseline_path.read_bytes()).hexdigest()!=research['baseline_report_sha256']:
            raise ValueError('Research baseline hash changed')
        baseline=json.loads(baseline_path.read_text(encoding='utf-8'))
        if records!=baseline['cities'] or additions!=baseline['added_cities']:
            raise ValueError('V2 baseline changed; existing IDs must remain stable')
        reviewed_ids=set()
        for decision in research['decisions']:
            native=decision['external_id']
            if native in reviewed_ids:raise ValueError('Duplicate research decision')
            reviewed_ids.add(native)
            record=next(r for r in records if r['provider']=='Lamasoo' and r['external_id']==native)
            validate_decision(decision,record,gaz.admin)
            geo=decision['geonames'];key=decision['admin1'];country=decision['country']
            if key not in province_keys:
                a=gaz.admin[key]
                province_keys[key]=dict(Id=next_province,Name=a['name'],NameEn=a['name_en'],Slug='geo-'+key.lower().replace('.','-'),
                    CountryCode=country,GeoNamesAdmin1Code=key,SortOrder=next_province,IsActive=True)
                next_province+=1
            province=province_keys[key]
            reported={province_names[normalize(h.get('province'))]['Id'] for h in obs[native] if normalize(h.get('province')) in province_names}
            if reported and reported!={province['Id']} and not decision.get('allow_inventory_province_conflict'):
                raise ValueError('Research contradicts Inventory province')
            exact=[c for c in cities if c['ProvinceId']==province['Id'] and normalize(c['Name'])==normalize(decision['canonical_name'])]
            aliases=[c for c in by_geo.get(geo['id'],[]) if c['ProvinceId']==province['Id']]
            if len(exact)>1 or not exact and len(aliases)>1:raise ValueError('Ambiguous canonical research target')
            target=exact[0] if exact else aliases[0] if aliases else None
            if not target:
                target=dict(Id=next_city,Name=decision['canonical_name'],NameEn=decision['name_en'],Slug='refahi-destination-'+str(next_city),
                    ProvinceId=province['Id'],SortOrder=next_city,IsActive=True,LocationKind=decision['location_kind'])
                cities.append(target);additions.append(target);next_city+=1
            entries=by_geo.setdefault(geo['id'],[])
            if all(c['Id']!=target['Id'] for c in entries):entries.append(target)
            countries.setdefault(country,dict(code=country,name=gaz.countries[country]['name'],name_en=gaz.countries[country]['name']))
            record.update(reference_id=target['Id'],province_id=province['Id'],country=country,method=decision['method'],reason=None)
            record['evidence']['research']=decision
            record['evidence']['canonical_province']=dict(id=province['Id'],country=country,admin1=key)
    # Province values: genuine native IDs must agree across every observed city.
    by_native={r['external_id']:r for r in records if r['provider']=='Lamasoo'}
    province_records=[]
    for state_id in sorted({c['state']['id'] for c in snapp}):
        group=[r for r in records if r['provider']=='SnappTrip' and r['evidence']['state']['id']==state_id]
        ids={r.get('province_id') for r in group}
        if None not in ids and len(ids)==1:
            province_records.append(dict(provider='SnappTrip',key=str(state_id),kind='native_id',province_id=next(iter(ids)),evidence={'city_ids':[r['external_id'] for r in group]}))
    groups=collections.defaultdict(list)
    for h in hotels:
        if h.get('province'):groups[h['province']].append(h)
    for value,items in groups.items():
        ids={by_native.get(h.get('city'),{}).get('province_id') for h in items}
        native=bool(re.fullmatch('[a-f0-9]{24}',value))
        if native and None not in ids and len(ids)==1:target=next(iter(ids))
        elif not native and normalize(value) in province_names and any(explicit_country([h])=='IR' for h in items): target=province_names[normalize(value)]['Id']
        else:continue
        province_records.append(dict(provider='Lamasoo',key=value,kind='native_id' if native else 'raw_value',province_id=target,evidence={'hotel_ids':[h['id'] for h in items]}))
    used={r['province_id'] for r in records if r['reference_id']}
    all_provinces=sorted([p for p in province_keys.values() if p['Id']<=31 or p['Id'] in used],key=lambda p:p['Id'])
    mappings=[r for r in records if r['reference_id']]
    assert len({(r['provider'],r['external_id']) for r in records})==len(records)
    assert len({(c['ProvinceId'],normalize(c['Name'])) for c in cities})==len(cities)
    assert not {m['old_id'] for m in merges}&{r['reference_id'] for r in mappings}
    report=dict(version=version,summary={p:dict(mapped=sum(r['reference_id'] is not None for r in records if r['provider']==p),unmapped=sum(r['reference_id'] is None for r in records if r['provider']==p)) for p in ['SnappTrip','Lamasoo']},
                merges=merges,added_cities=additions,added_provinces=[p for p in all_provinces if p['Id']>31],countries=list(countries.values()),cities=records,province_mappings=province_records,
                sources=[dict(file=str(f),sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in [args.dump]+[args.snapshot/n for n in ['snapptrip-cities.raw.json','lamasoo-cities.raw.json','lamasoo-inventory.raw.json']]+[args.gazetteer/n for n in ['cities500.zip','IQ.zip','admin1CodesASCII.txt','countryInfo.txt']]+sorted(args.flight_catalog.glob('airports-*.json'))])
    if research_path:
        report['sources'].append(dict(file=str(research_path),sha256=hashlib.sha256(research_path.read_bytes()).hexdigest()))
    (out/'mapping-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (out/'cities-review.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,['provider','external_id','name','code','reason']);writer.writeheader()
        writer.writerows({k:r.get(k) for k in writer.fieldnames} for r in records if not r['reference_id'])
    with (out/'new-cities.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,['Id','Name','NameEn','CountryCode','ProvinceId','ProvinceName','LocationKind']);writer.writeheader()
        for c in additions:
            p=next(p for p in all_provinces if p['Id']==c['ProvinceId'])
            writer.writerow({**{k:c.get(k) for k in writer.fieldnames},'CountryCode':p['CountryCode'],'ProvinceName':p['Name']})
    def literal(v):
        if v is None:return 'NULL'
        if isinstance(v,bool):return 'true' if v else 'false'
        if isinstance(v,int):return str(v)
        if isinstance(v,(dict,list)):v=json.dumps(v,ensure_ascii=False,separators=(',',':'))
        return "'"+str(v).replace("'","''")+"'"
    def values(rows):return ',\n'.join('('+','.join(map(literal,row))+')' for row in rows)+';'
    replacements={
        'ORIGINAL_CITIES':values((c['Id'],c['Name'],c['ProvinceId']) for c in original),
        'ORIGINAL_PROVINCES':values((p['Id'],p['Name']) for p in provinces),
        'MERGES':values((m['old_id'],m['new_id'],m['before']) for m in merges),
        'COUNTRIES':values((c['code'],c['name'],c['name_en']) for c in countries.values()),
        'PROVINCES':values((p['Id'],p['Name'],p['NameEn'],p['Slug'],p['CountryCode'],p['GeoNamesAdmin1Code'],p['SortOrder']) for p in all_provinces),
        'NEW_CITIES':values((c['Id'],c['Name'],c['NameEn'],c['Slug'],c['ProvinceId'],c['SortOrder'],c['LocationKind']) for c in additions),
        'CITY_MAPPINGS':values((r['provider'],r['external_id'],r['reference_id'],r['name'],r['code'],r['country'],r['method'],r['evidence']) for r in mappings),
        'PROVINCE_MAPPINGS':values((r['provider'],r['key'],r['kind'],r['province_id'],r['evidence']) for r in province_records)}
    template=Path(__file__).with_name('mapping-v2-template.sql').read_text(encoding='utf-8')
    for key,value in replacements.items():template=template.replace('@@'+key+'@@',value)
    assert '@@' not in template
    if research_path:template=template.replace('-- V2, 2026-10-19. Supersedes the V1 mapping-only script.', '-- V3, 2026-10-19. Includes reviewed evidence; preserves V2 allocated IDs. Supports V1/V2 upgrade.')
    (out/f'2026-10-19-v{version}-reference-geography.sql').write_text(template,encoding='utf-8')
    print(json.dumps(dict(summary=report['summary'],new_cities=len(additions),new_provinces=len(report['added_provinces']),countries=len(countries),merges=len(merges)),ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--dump',type=Path,required=True);parser.add_argument('--snapshot',type=Path,required=True)
    parser.add_argument('--gazetteer',type=Path,required=True)
    parser.add_argument('--flight-catalog',type=Path,default=Path('src/Refahi.Modules.Flights.Infrastructure/Data/search-catalog-20260906'))
    parser.add_argument('--output',type=Path,default=Path(__file__).parent/'2026-10-19-v2')
    parser.add_argument('--decisions',type=Path)
    build(parser.parse_args())
