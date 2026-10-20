"""Resolve snapshot candidates using recorded, reproducible geographical evidence."""
import argparse
import collections
import csv
import hashlib
import json
import math
import re
import unicodedata
import zipfile
from pathlib import Path
from build_mapping import normalize, read_reference_rows


def latin_key(value):
    return normalize(''.join(c for c in unicodedata.normalize('NFKD',value or '') if not unicodedata.combining(c)))


def airport_matches_place(airport, geo):
    if geo['feature_class']!='P' or geo['feature'] in {'PPLQ','PPLF','PPLH','PPLW'}:return False
    municipality=airport['municipality'] or re.sub(r'\b(International|Airport)\b','',airport['name'],flags=re.I).strip()
    labels={latin_key(n) for n in geo['names']}
    if latin_key(municipality) not in labels and not (
        geo['feature'].startswith('PPLA') and latin_key(airport['iata_code']) in labels):return False
    if not airport['latitude_deg'] or not airport['longitude_deg']:return False
    a,b=map(math.radians,[float(airport['latitude_deg']),geo['latitude']])
    dl=math.radians(float(airport['longitude_deg'])-geo['longitude'])
    distance=6371*2*math.asin(min(1,math.sqrt(math.sin((a-b)/2)**2+math.cos(a)*math.cos(b)*math.sin(dl/2)**2)))
    return distance<=50


def load_places(archives, wanted):
    index=collections.defaultdict(dict); counties={}
    wanted.discard('')
    for path,member in archives:
        source_hash=hashlib.sha256(path.read_bytes()).hexdigest()
        with zipfile.ZipFile(path) as archive:
            for line in archive.read(member).decode('utf-8').splitlines():
                f=line.split('\t')
                labels=[f[1],f[2]]+f[3].split(',')
                names={normalize(n) for n in labels}; names.discard('')
                if f[7]=='ADM2':
                    counties[f[8]+'.'+f[10]+'.'+f[11]]={normalize(re.sub(r'^شهرستان\s*','',n)) for n in labels}
                for name in names & wanted:
                    index[name][int(f[0])]=dict(id=int(f[0]),name=f[1],name_en=f[2],country=f[8],admin1=f[8]+'.'+f[10],
                        admin2=f[11],feature_class=f[6],feature=f[7],latitude=float(f[4]),longitude=float(f[5]),
                        names=sorted(names),source_url='https://download.geonames.org/export/dump/'+path.name,
                        source_file=str(path),source_sha256=source_hash)
    return index,counties


def select_city(places, county=None, county_names=None):
    rows={p['id']:p for p in places if p['feature_class']=='P' and p['feature'] not in {'PPLQ','PPLF','PPLH','PPLW'}}
    if county and county_names:
        scoped={key:p for key,p in rows.items() if normalize(county) in county_names.get(p['admin1']+'.'+p['admin2'],set())}
        if scoped: rows=scoped
        elif rows and all(county_names.get(p['admin1']+'.'+p['admin2']) for p in rows.values()):return None
    if len(rows)==1:return next(iter(rows.values()))
    # Only use an administrative-seat discriminator when the source explicitly calls this a city.
    seats=[p for p in rows.values() if p['feature'].startswith('PPLA') or p['feature']=='PPLC']
    return seats[0] if len(seats)==1 else None


def write_review_queue(output, outcomes, baseline, proposals):
    native={c['external_id']:c for c in baseline['cities'] if c['provider']=='Lamasoo'}
    candidates={c['external_id']:c for c in proposals['records']};items=[]
    for row in outcomes:
        if row['status']=='resolved':continue
        proposal=candidates[row['external_id']];inventory=native[row['external_id']]['evidence'].get('inventory',[])
        question='کشور و استان با شناسه و نام، نوع مقصد، مختصات مرکز مقصد، وضعیت فعال بودن و شناسه جایگزین در صورت ادغام را اعلام کنید.'
        if row['status']=='exclude_invalid_record':question='آیا این مقصد تستی/غیرفعال است؟ وضعیت و شناسه مقصد جایگزین احتمالی را اعلام کنید.'
        if row['status']=='province_record_not_city':question='این شناسه شهر است یا استان/منطقه؟ نوع مقصد و محدوده آن را مشخص کنید؛ به‌عنوان شهر نگاشت نشده است.'
        items.append(dict(**row,original_status=proposal['status'],candidate_count=len(proposal['candidates']),candidates=proposal['candidates'],
            inventory=inventory,hotel_count=len(inventory),requested_information=question,provider_confirmation_received=False))
    items.sort(key=lambda x:(-x['hotel_count'],x['name'],x['external_id']))
    (output/'provider-review.json').write_text(json.dumps(dict(snapshot_date='2026-10-10',review_date='2026-10-19',
        note='Research queue; no requests have been sent to Lamasoo. Further independent research may resolve more items.',records=items),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    fields=['external_id','name','code','status','original_status','hotel_count','candidate_count','requested_information']
    with (output/'provider-review.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fields);writer.writeheader();writer.writerows({k:x.get(k) for k in fields} for x in items)


def build(args):
    args.output.mkdir(parents=True,exist_ok=True)
    baseline=json.loads(args.baseline.read_text(encoding='utf-8'))
    proposals=json.loads(args.proposals.read_text(encoding='utf-8'))
    text=args.dump.read_text(encoding='utf-8-sig'); refs=read_reference_rows(text,'cities'); provinces=read_reference_rows(text,'provinces')
    refp={normalize(p['Name']):p for p in provinces}
    admin={}; province_code={}
    for line in (args.gazetteer/'admin1CodesASCII.txt').read_text(encoding='utf-8').splitlines():
        f=line.split('\t');admin[f[0]]=dict(key=f[0],name=f[1],name_en=f[2])
        if f[0].startswith('IR.'):
            p=next(p for p in provinces if normalize(p['NameEn'])==normalize(f[2].removesuffix(' Province')))
            province_code[p['Id']]=f[0]
    community_counties={c['id']:c for c in json.loads((args.sources/'counties.json').read_text(encoding='utf-8'))}
    community_cities={c['id']:c for c in json.loads((args.sources/'cities.json').read_text(encoding='utf-8'))}
    source_meta=json.loads((args.sources/'sources.json').read_text(encoding='utf-8'))
    city_source=next(f for f in source_meta['files'] if f['file']=='cities.json')
    official=json.loads(args.official.read_text(encoding='utf-8'))
    wanted={normalize(r['name']) for r in proposals['records']}|{normalize(c['NameEn']) for c in refs}
    wanted.update(normalize(x['english']) for x in official['places'])
    archives=[(args.gazetteer/'cities500.zip','cities500.txt')]
    archives.extend((args.sources/(country+'.zip'),country+'.txt') for country in ['IR','TR','AE','ID','MY','LK'])
    index,county_names=load_places(archives,wanted)
    def rows(name):return list(index.get(normalize(name),{}).values())
    decisions={}; outcomes=[]
    def approve(candidate,geo,method,evidence,canonical_name=None,kind='city',allow_conflict=False):
        assert geo['admin1'] in admin and geo['country']==geo['admin1'].split('.')[0]
        existing=next(c for c in baseline['cities'] if c['provider']=='Lamasoo' and c['external_id']==candidate['external_id'])
        assert existing['reference_id'] is None
        reported={refp[normalize(h.get('province'))]['Id'] for h in existing['evidence'].get('inventory',[]) if normalize(h.get('province')) in refp}
        selected_province=next((key for key,value in province_code.items() if value==geo['admin1']),None)
        if reported and reported!={selected_province} and not allow_conflict:return
        iran_literals={'iran','ir',normalize('ایران'),'6345899296299b65e13c7b95'}
        if geo['country']!='IR' and any(normalize(h.get('country')) in iran_literals for h in existing['evidence'].get('inventory',[])) and not allow_conflict:return
        decisions[candidate['external_id']]=dict(external_id=candidate['external_id'],expected_name=candidate['name'],expected_code=candidate['code'],
            country=geo['country'],admin1=geo['admin1'],canonical_name=canonical_name or candidate['name'],name_en=geo['name_en'],
            location_kind=kind,method=method,geonames={k:v for k,v in geo.items() if k!='names'},evidence=evidence,
            allow_inventory_province_conflict=allow_conflict,review_date='2026-10-19')
    for c in proposals['records']:
        result=dict(external_id=c['external_id'],name=c['name'],code=c['code'],status='needs_provider_or_more_evidence')
        if c['status'] in {'exclude_invalid_record','province_record_not_city'}:
            result['status']=c['status'];outcomes.append(result);continue
        if c['status']=='single_iran_source_candidate':
            target=c['candidates'][0]
            if target.get('kind')=='city':
                province=refp[normalize(target['province_name'])]; pk=province_code[province['Id']]
                matches=[g for g in rows(c['name']) if g['country']=='IR' and g['admin1']==pk]
                english_bridge=None
                if not matches:
                    existing=[r for r in refs if r['ProvinceId']==province['Id'] and normalize(r['Name'])==normalize(c['name'])]
                    if len(existing)==1:
                        english_bridge=existing[0]['NameEn'];matches=[g for g in rows(english_bridge) if g['country']=='IR' and g['admin1']==pk]
                # Foreign homonyms require an independent provider-country discriminator.
                foreign=[g for g in rows(c['name']) if g['feature_class']=='P' and g['country']!='IR']
                county=community_counties.get(community_cities.get(target['source_id'],{}).get('county_id'),{}).get('name')
                selected=select_city(matches,county,county_names)
                if selected and not foreign:
                    approve(c,selected,'iran_community_city_list_and_geonames',[
                        dict(url=city_source['url'],sha256=city_source['sha256'],record_id=target['source_id'],province=target['province_name'],county=county),
                        dict(url=selected['source_url'],sha256=selected['source_sha256'],record_id=selected['id'],english_bridge=english_bridge)],canonical_name=target['name'])
                else:result['detail']='foreign_homonym' if foreign else 'second_source_ambiguous_or_missing'
        if c['external_id'] in decisions:result['status']='resolved_two_geographic_sources'
        outcomes.append(result)

    # IATA is usable only when code, municipality name AND independently identified province agree.
    with (args.sources/'regions.csv').open(encoding='utf-8-sig',newline='') as stream:
        regions={r['code']:r for r in csv.DictReader(stream)}
    with (args.sources/'airports.csv').open(encoding='utf-8-sig',newline='') as stream:
        airports=collections.defaultdict(list)
        for row in csv.DictReader(stream):
            if row['iata_code']:airports[row['iata_code']].append(row)
    for c in proposals['records']:
        if c['external_id'] in decisions or c['status'] in {'exclude_invalid_record','province_record_not_city'}:continue
        code=''.join(x for x in c['code'] or '' if x.isascii() and x.isalpha()).upper()
        found=[]
        for airport in airports.get(code,[]):
            if airport['iso_country']!='IR':continue
            region=regions.get(airport['iso_region'],{}).get('name','')
            pp=[p for p in provinces if latin_key(p['NameEn'])==latin_key(region.removesuffix(' Province'))]
            if len(pp)!=1:continue
            for geo in rows(c['name']):
                if geo['country']=='IR' and geo['admin1']==province_code[pp[0]['Id']] and airport_matches_place(airport,geo):
                    found.append((geo,airport))
        unique={geo['id']:(geo,airport) for geo,airport in found}
        if len(unique)==1:
            geo,airport=next(iter(unique.values()))
            approve(c,geo,'matching_iata_municipality_and_geonames',[
                dict(url='https://davidmegginson.github.io/ourairports-data/airports.csv',sha256=hashlib.sha256((args.sources/'airports.csv').read_bytes()).hexdigest(),record_id=airport['id'],iata=code,municipality=airport['municipality'],airport_name=airport['name'],region=airport['iso_region'],latitude=airport['latitude_deg'],longitude=airport['longitude_deg'],maximum_distance_km=50),
                dict(url=geo['source_url'],sha256=geo['source_sha256'],record_id=geo['id'])],kind='city' if geo['feature_class']=='P' else 'destination')
    for item in official['places']:
        target=[c for c in proposals['records'] if c['name']==item['native_name']]
        for c in target:
            if c['external_id'] in decisions:continue
            found=[g for g in rows(item['english']) if g['country']==item['country'] and g['admin1']==item['admin1']]
            if item.get('feature'):found=[g for g in found if g['feature']==item['feature']]
            geo=select_city(found) if item.get('kind','city')=='city' else (found[0] if len(found)==1 else None)
            if geo:
                code=''.join(x for x in c['code'] or '' if x.isascii() and x.isalpha()).upper()
                matching_airports=[a for a in airports.get(item['required_iata'],[]) if a['iso_country']==geo['country'] and airport_matches_place(a,geo)]
                if code!=item['required_iata'] or len(matching_airports)!=1:continue
                airport=matching_airports[0]
                approve(c,geo,'official_tourism_and_geonames',[
                    dict(url=item['source_url'],fact=item['fact'],native_caption_alias=item['native_name'],english_name=item['english']),
                    dict(url='https://davidmegginson.github.io/ourairports-data/airports.csv',sha256=hashlib.sha256((args.sources/'airports.csv').read_bytes()).hexdigest(),record_id=airport['id'],iata=code,municipality=airport['municipality'],region=airport['iso_region'],latitude=airport['latitude_deg'],longitude=airport['longitude_deg'],maximum_distance_km=50),
                    dict(url=geo['source_url'],sha256=geo['source_sha256'],record_id=geo['id'])],kind=item.get('kind','city'))
    inventory_path=args.inventory
    inventory_hash=hashlib.sha256(inventory_path.read_bytes()).hexdigest()
    assert inventory_hash==next(s['sha256'] for s in baseline['sources'] if Path(s['file']).name=='lamasoo-inventory.raw.json')
    inventory={h['id']:h for h in json.loads(inventory_path.read_text(encoding='utf-8-sig'))['hotels']}
    for item in official.get('inventory_address_cases',[]):
        c=next(c for c in proposals['records'] if c['external_id']==item['native_city_id'])
        hotel=inventory[item['hotel_id']]
        assert hotel['city']==c['external_id'] and hotel['address'].startswith(item['expected_address_prefix'])
        # This is a manually reviewed province prefix, never a substring such as Tehran Street.
        assert item['expected_address_prefix'].startswith(item['province_name']+' ')
        province=refp[normalize(item['province_name'])];pk=province_code[province['Id']]
        candidates=[v for v in community_cities.values() if normalize(v['name'])==normalize(c['name']) and
            any(x.get('province_name')==item['province_name'] and x.get('source_id')==v['id'] for x in c['candidates'])]
        if len(candidates)!=1:continue
        candidate=candidates[0];county=community_counties[candidate['county_id']]['name']
        geo=select_city([g for g in rows(c['name']) if g['country']=='IR' and g['admin1']==pk],county,county_names)
        if geo:
            approve(c,geo,'inventory_address_register_and_geonames',[
                dict(source_file=str(inventory_path),sha256=inventory_hash,hotel_id=hotel['id'],address=hotel['address']),
                dict(url=city_source['url'],sha256=city_source['sha256'],record_id=candidate['id'],province=item['province_name'],county=county),
                dict(url=geo['source_url'],sha256=geo['source_sha256'],record_id=geo['id'])],canonical_name=candidate['name'])
    # The conflicting Astara hotel is identified by an exact address on the hotel's own website.
    astara=next(c for c in proposals['records'] if c['external_id']=='5ae5ab74734a9af40a7ab325')
    geo=next(g for g in rows('آستارا') if g['id']==142676)
    approve(astara,geo,'official_hotel_identity_and_address',astara['candidates']+[
        dict(url=geo['source_url'],sha256=geo['source_sha256'],record_id=geo['id'])],allow_conflict=True)
    for row in outcomes:
        if row['external_id'] in decisions:
            row['status']='resolved';row['method']=decisions[row['external_id']]['method'];row['country']=decisions[row['external_id']]['country'];row['admin1']=decisions[row['external_id']]['admin1']
    sources=[args.baseline,args.proposals,args.official,inventory_path,args.gazetteer/'admin1CodesASCII.txt',args.gazetteer/'countryInfo.txt',args.sources/'cities.json',args.sources/'provinces.json',args.sources/'counties.json',args.sources/'airports.csv',args.sources/'regions.csv']+[a for a,m in archives]
    data=dict(version=1,baseline_report_sha256=hashlib.sha256(args.baseline.read_bytes()).hexdigest(),
        source_files=[dict(file=str(f),sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in sources],
        summary=dict(collections.Counter(x['status'] for x in outcomes)),decisions=list(decisions.values()),outcomes=outcomes)
    assert len(outcomes)==256 and len({x['external_id'] for x in outcomes})==256
    (args.output/'resolutions.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (args.output/'remaining.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        fields=['external_id','name','code','status','detail']; writer=csv.DictWriter(stream,fields);writer.writeheader()
        for row in outcomes:
            if row['status']!='resolved':writer.writerow({k:row.get(k) for k in fields})
    write_review_queue(args.output,outcomes,baseline,proposals)
    print(json.dumps(data['summary'],ensure_ascii=False))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--dump',type=Path,required=True);p.add_argument('--sources',type=Path,required=True);p.add_argument('--gazetteer',type=Path,required=True)
    p.add_argument('--inventory',type=Path,required=True,help='Archived lamasoo-inventory.raw.json matching the V2 source hash')
    root=Path(__file__).parent;p.add_argument('--baseline',type=Path,default=root/'2026-10-19-v2/mapping-report.json')
    p.add_argument('--proposals',type=Path,default=root/'2026-10-19-v2/internet-review/research-candidates.json')
    p.add_argument('--official',type=Path,default=root/'official-geography-evidence.json');p.add_argument('--output',type=Path,default=root/'2026-10-19-v3')
    build(p.parse_args())
