"""Read-only per-city API audit. Credentials are read locally and never written to reports."""
import argparse
import collections
import csv
import datetime
import hashlib
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def hotels_for_city(payload, city_id):
    if not isinstance(payload,dict) or not isinstance(payload.get('hotels'),list):
        raise ValueError('unexpected_inventory_shape')
    hotels=payload['hotels']
    if any(not isinstance(h,dict) or not isinstance(h.get('id'),str) or not h['id'] for h in hotels):
        raise ValueError('invalid_hotel_identity')
    if any(h.get('city')!=city_id for h in hotels):raise ValueError('city_filter_mismatch')
    if len({h['id'] for h in hotels})!=len(hotels):raise ValueError('duplicate_hotel_identity')
    return hotels


def run(args):
    review=json.loads(args.review.read_text(encoding='utf-8'))['records']
    assert len({r['external_id'] for r in review})==len(review)
    assert all(re.fullmatch('[a-f0-9]{24}',r['external_id']) for r in review)
    config=json.loads(args.config.read_text(encoding='utf-8-sig'))['Hotels']['Providers']['Lamasoo']
    if config['BaseUrl'].rstrip('/')!='https://api.lamasoo.com':raise ValueError('Unexpected API host')
    key=config['ApiKey']
    if not key or '\n' in key or '\r' in key:raise ValueError('Missing or invalid configured API key')
    opener=urllib.request.build_opener(NoRedirect())
    manifest_path=args.snapshot/'manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    for entry in manifest['requests']:
        if entry.get('sha256'):
            assert hashlib.sha256((args.snapshot/entry['file']).read_bytes()).hexdigest()==entry['sha256']
    global_hotels=json.loads((args.snapshot/'lamasoo-inventory.raw.json').read_text(encoding='utf-8'))['hotels']
    cities=json.loads((args.snapshot/'lamasoo-cities.raw.json').read_text(encoding='utf-8'))['cities']
    current_cities={c['id']:c for c in cities};global_by_city=collections.defaultdict(set)
    for hotel in global_hotels:global_by_city[hotel.get('city')].add(hotel['id'])
    args.output.mkdir(parents=True,exist_ok=True);records=[]
    for i,city in enumerate(review,1):
        cid=city['external_id'];file='lamasoo-inventory-city-'+cid+'.raw.json';path=args.snapshot/file
        url='https://api.lamasoo.com/booking/hotel_inventory?'+urllib.parse.urlencode({'city_id':cid})
        entry=next((x for x in manifest['requests'] if x.get('requested_city_id')==cid),None)
        record=dict(city_id=cid,name=city['name'],code=city['code'],mapping_status=city['status'],
            city_present_in_current_catalog=cid in current_cities,previous_hotel_count=city['hotel_count'],
            global_inventory_hotel_count=len(global_by_city[cid]),hotel_count=None,result='unknown',hotels=[],error=None)
        try:
            if entry is None:
                request=urllib.request.Request(url,headers={'Authorization':key,'Accept':'application/json'})
                with opener.open(request,timeout=30) as response:raw=response.read();status=response.status
                path.write_bytes(raw)
                entry=dict(url=url,method='GET',status=status,file=file,requested_city_id=cid,
                    retrieved_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sha256=hashlib.sha256(raw).hexdigest())
                manifest['requests'].append(entry)
                manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
                time.sleep(.25)
            hotels=hotels_for_city(json.loads(path.read_text(encoding='utf-8')),cid)
            record.update(hotel_count=len(hotels),result='has_hotels' if hotels else 'no_hotels_in_response',
                matches_global_inventory={h['id'] for h in hotels}==global_by_city[cid],
                checked_at_utc=entry['retrieved_at_utc'],response_file=str(path),response_sha256=entry['sha256'],
                hotels=[{k:h.get(k) for k in ['id','name','english_name','city','province','country','address','latitude','longitude','website']} for h in hotels])
        except urllib.error.HTTPError as error:record['error']='HTTP_'+str(error.code)
        except (urllib.error.URLError,TimeoutError):record['error']='network_or_timeout'
        except (ValueError,KeyError):record['error']='invalid_or_unexpected_response'
        records.append(record)
        if i%25==0:print(json.dumps(dict(checked=i,total=len(review))),flush=True)
    records.sort(key=lambda x:(-(x['hotel_count'] or 0),x['name'],x['city_id']))
    report=dict(checked_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='All unresolved Lamasoo city records in V3',
        source_review=str(args.review),source_review_sha256=hashlib.sha256(args.review.read_bytes()).hexdigest(),
        snapshot=str(args.snapshot),catalog_city_count=len(cities),global_inventory_hotel_count=len(global_hotels),
        summary=dict(collections.Counter(x['result'] for x in records)),
        distinct_hotels_in_unresolved_cities=len({h['id'] for r in records for h in r['hotels']}),
        limitations=['Inventory presence does not establish availability, current price, active sale status or successful booking.',
                     'An empty authenticated API response establishes no hotels returned for this account at this time; it does not prove physical absence of hotels.',
                     'No Availability, Submit or Confirm calls and no database changes were performed.'],records=records)
    (args.output/'inventory-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    fields=['city_id','name','code','mapping_status','result','hotel_count','previous_hotel_count','global_inventory_hotel_count','matches_global_inventory','city_present_in_current_catalog','checked_at_utc','error']
    for filename,rows in [('cities.csv',records),('cities-with-hotels.csv',[r for r in records if r['result']=='has_hotels'])]:
        with (args.output/filename).open('w',encoding='utf-8-sig',newline='') as stream:
            writer=csv.DictWriter(stream,fields);writer.writeheader();writer.writerows({k:r.get(k) for k in fields} for r in rows)
    with (args.output/'hotels.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        fields=['city_id','city_name','hotel_id','hotel_name','country','province','address','latitude','longitude','website']
        writer=csv.DictWriter(stream,fields);writer.writeheader()
        for r in records:
            for h in r['hotels']:
                writer.writerow(dict(city_id=r['city_id'],city_name=r['name'],hotel_id=h['id'],hotel_name=h['name'],**{k:h[k] for k in fields[4:]}))
    print(json.dumps(dict(summary=report['summary'],hotels=report['distinct_hotels_in_unresolved_cities'],output=str(args.output)),ensure_ascii=False),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();root=Path(__file__).parent
    parser.add_argument('--snapshot',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--review',type=Path,default=root/'2026-10-19-v3/provider-review.json')
    parser.add_argument('--config',type=Path,default=Path('src/Refahi.Api/appsettings.json'))
    run(parser.parse_args())
