"""Create an additive V3 upgrade from explicit user decisions; no API or DB access."""
import collections
import argparse
import csv
import hashlib
import json
from pathlib import Path
from build_mapping import normalize, read_reference_rows

ROOT=Path(__file__).parent
COUNTRIES={'IR':('ایران','Iran'),'TR':('ترکیه','Turkey'),'AE':('امارات متحده عربی','United Arab Emirates'),
    'IQ':('عراق','Iraq'),'AZ':('جمهوری آذربایجان','Azerbaijan'),'OM':('عمان','Oman'),'TH':('تایلند','Thailand'),
    'MY':('مالزی','Malaysia'),'GE':('گرجستان','Georgia'),'AM':('ارمنستان','Armenia'),'GB':('بریتانیا','United Kingdom'),
    'RU':('روسیه','Russia'),'QA':('قطر','Qatar'),'CY':('قبرس','Cyprus'),'ID':('اندونزی','Indonesia')}


def literal(value):
    if value is None:return 'NULL'
    if isinstance(value,bool):return 'true' if value else 'false'
    if isinstance(value,int):return str(value)
    if isinstance(value,(dict,list)):value=json.dumps(value,ensure_ascii=False,separators=(',',':'))
    return "'"+value.replace("'","''")+"'"


def values(rows):return ',\n'.join('('+','.join(literal(x) for x in row)+')' for row in rows)+';'


def prepare(baseline,decisions,cities,provinces,audit):
    refs={c['Id']:dict(c) for c in cities};ps={p['Id']:dict(p) for p in provinces}
    foreign=collections.defaultdict(list)
    for p in provinces:
        if p.get('CountryCode','IR')!='IR':foreign[p['CountryCode']].append(p['Id'])
    buckets={code:min(ids) for code,ids in foreign.items()}
    next_province=max(ps)+1
    for decision in decisions['records']:
        country=decision['country']
        if country not in COUNTRIES:raise ValueError('Unknown reviewed country')
        if country!='IR' and country not in buckets:buckets[country]=next_province;next_province+=1
    old_provinces={p['Id']:buckets[p['CountryCode']] for p in provinces if p.get('CountryCode','IR')!='IR'}
    for c in refs.values():c['ProvinceId']=old_provinces.get(c['ProvinceId'],c['ProvinceId'])
    native={r['external_id']:r for r in baseline['cities'] if r['provider']=='Lamasoo'}
    evidence={r['city_id']:r for r in audit['records']};next_city=max(refs)+1;result=[];additions=[];seen=set()
    for decision in decisions['records']:
        cid=decision['city_id']
        if cid in seen:raise ValueError('Duplicate user decision')
        seen.add(cid);source=native[cid]
        if source['reference_id'] is not None or source['name']!=decision['native_name']:raise ValueError('Stale or already mapped decision')
        observed=evidence[cid]
        if not any(h['id']==decision['hotel_id'] for h in observed['hotels']):raise ValueError('Hotel is not attached to the reviewed city')
        country=decision['country'];province=decision['province_id'] if country=='IR' else buckets[country]
        target=refs.get(decision.get('reference_city_id'))
        if target and (target['ProvinceId']!=province or normalize(target['Name'])!=normalize(decision['name'])):
            # Existing Dubai spelling and Bandar Asaluyeh are deliberate aliases confirmed by ID.
            if not (target['ProvinceId']==province and cid=='63fb45c108425256687889b1' and target['Id']==1336
                    and normalize(target['Name'])==normalize('دوبی') and normalize(decision['name'])==normalize('دبی')):
                raise ValueError('Canonical city/province mismatch')
        if decision.get('reference_city_id') and target is None:raise ValueError('Missing reviewed canonical city')
        if not target:
            candidates=[c for c in refs.values() if c['ProvinceId']==province and normalize(c['Name'])==normalize(decision['name'])]
            if len(candidates)>1:raise ValueError('Ambiguous canonical city')
            target=candidates[0] if candidates else dict(Id=next_city,Name=decision['name'],NameEn=decision['name_en'],ProvinceId=province,
                Slug='refahi-destination-'+str(next_city),SortOrder=next_city,IsActive=True,LocationKind=decision['kind'])
            if not candidates:refs[next_city]=target;additions.append(target);next_city+=1
        if source['name']=='غیرفعال' and decision['enabled']:raise ValueError('Identity decision cannot activate a disabled destination')
        proof=dict(authority=decisions['authority'],confirmed_at=decisions['confirmed_at'],decision=decision,
            raw_inventory_hotels=observed['hotels'],inventory_checked_at_utc=observed['checked_at_utc'],previous_reason=source['reason'])
        result.append(dict(provider_city_id=cid,city_id=target['Id'],name=source['name'],code=source['code'],country=country,
            enabled=decision['enabled'],evidence=proof,canonical_name=target['Name'],province_id=province))
    return buckets,old_provinces,result,additions


def main(dump=None,output=None):
    out=output or ROOT/'2026-10-19-v4';out.mkdir(parents=True,exist_ok=True)
    paths=[ROOT/'2026-10-19-v3/mapping-report.json',ROOT/'user-confirmed-destinations.json',ROOT/'2026-10-19-live-inventory/inventory-audit.json']
    baseline,decisions,audit=[json.loads(p.read_text(encoding='utf-8')) for p in paths]
    dump=dump or ROOT/'fixtures/reference-geography.sql'
    source=dump.read_text(encoding='utf-8-sig');cities=read_reference_rows(source,'cities')
    retired={m['old_id'] for m in baseline['merges']};cities=[c for c in cities if c['Id'] not in retired]+baseline['added_cities']
    provinces=read_reference_rows(source,'provinces')+baseline['added_provinces']
    buckets,merges,mappings,additions=prepare(baseline,decisions,cities,provinces,audit)
    countries=[(code,*COUNTRIES[code]) for code in sorted(set(buckets)|{'IR'})]
    new_bucket=[(pid,code,*COUNTRIES[code]) for code,pid in buckets.items() if pid not in {p['Id'] for p in provinces}]
    substitutions=dict(COUNTRIES=values(countries),BUCKETS=values((pid,code,*COUNTRIES[code]) for code,pid in buckets.items()),
        NEW_BUCKETS=values(new_bucket),PROVINCES=values((p['Id'],p['Name'],p['CountryCode'],merges[p['Id']]) for p in baseline['added_provinces']),
        NEW_CITIES=values((c['Id'],c['Name'],c['NameEn'],c['ProvinceId'],c['Slug'],c['SortOrder'],c['LocationKind']) for c in additions),
        MAPPINGS=values((m['provider_city_id'],m['city_id'],m['name'],m['code'],m['country'],m['enabled'],m['evidence'],m['canonical_name'],m['province_id']) for m in mappings))
    template=(ROOT/'mapping-v4-template.sql').read_text(encoding='utf-8')
    for key,value in substitutions.items():template=template.replace('@@'+key+'@@',value)
    assert '@@' not in template
    # Published V4 SQL uses CRLF; preserve its production SHA-256 on every OS.
    (out/'2026-10-19-v4-upgrade.sql').write_text(template,encoding='utf-8',newline='\r\n')
    v3_path=ROOT/'2026-10-19-v3/2026-10-19-v3-reference-geography.sql'
    def body(sql):
        lines=sql.splitlines();lines.remove('BEGIN;')
        assert lines[-1]=='COMMIT;';return '\n'.join(lines[:-1])
    v3=body(v3_path.read_text(encoding='utf-8'))
    assert '$v3_source$' not in v3
    complete='-- V4 complete installation/upgrade; one transaction, safe to replay.\nBEGIN;\n'
    complete+='SELECT pg_advisory_xact_lock(174593,20261019);\nDO $bootstrap$ BEGIN\n'
    complete+='IF to_regclass(\'"references".province_reorganization_history\') IS NULL THEN\nEXECUTE $v3_source$\n'+v3+'\n$v3_source$;\nEND IF;\nEXCEPTION WHEN OTHERS THEN RAISE EXCEPTION \'V3 bootstrap failed: %\',SQLERRM;\nEND $bootstrap$;\n'
    complete+=body(template)+'\nCOMMIT;\n'
    (out/'2026-10-19-v4-complete.sql').write_text(complete,encoding='utf-8',newline='\r\n')
    remaining=json.loads((ROOT/'2026-10-19-v3/provider-review.json').read_text(encoding='utf-8'))['records']
    resolved={m['provider_city_id'] for m in mappings};remaining=[r for r in remaining if r['external_id'] not in resolved]
    report=dict(version=4,prerequisite='V3',authority=decisions['authority'],country_buckets=buckets,province_reorganizations=merges,
        added_cities=additions,mappings=mappings,remaining=remaining,summary=dict(lamasoo_mapped=baseline['summary']['Lamasoo']['mapped']+len(mappings),
            lamasoo_unmapped=len(remaining),new_mappings=len(mappings),disabled_identity_mappings=sum(not m['enabled'] for m in mappings)),
        pending=decisions['pending'],sources=[dict(file=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths+[dump,v3_path,ROOT/'mapping-v4-template.sql']])
    (out/'mapping-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for file,rows,fields in [('confirmed-mappings.csv',mappings,['provider_city_id','name','canonical_name','city_id','country','province_id','enabled']),
                            ('remaining.csv',remaining,['external_id','name','code','status','hotel_count'])]:
        with (out/file).open('w',encoding='utf-8-sig',newline='') as stream:
            writer=csv.DictWriter(stream,fields);writer.writeheader();writer.writerows({k:r.get(k) for k in fields} for r in rows)
    print(json.dumps(report['summary']))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dump',type=Path,default=ROOT/'fixtures/reference-geography.sql')
    parser.add_argument('--output',type=Path,default=ROOT/'2026-10-19-v4')
    args=parser.parse_args();main(args.dump,args.output)
