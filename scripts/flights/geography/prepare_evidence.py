"""Reduce archived OurAirports/GeoNames public sources to reproducible geography-only evidence."""
import argparse
import csv
import gzip
import hashlib
import io
import json
import unicodedata
import zipfile
from pathlib import Path
from snapshot import write_json


def normalize(value):
    text = unicodedata.normalize('NFKD', value or '').translate(str.maketrans('يك', 'یک'))
    return ''.join(c for c in text if c.isalnum()).casefold()


def load(path):
    raw = path.read_bytes()
    return json.loads(gzip.decompress(raw) if path.suffix == '.gz' else raw)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--snapshot', type=Path, required=True)
    p.add_argument('--airports', type=Path, required=True)
    p.add_argument('--cities', type=Path, required=True)
    p.add_argument('--countries', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    snapshot = load(a.snapshot)
    codes = {r['iata_code'] for r in snapshot['airports']}
    airports = [r for r in csv.DictReader(io.StringIO(a.airports.read_text(encoding='utf-8-sig')))
        if r['iata_code'] in codes]
    names = {normalize(r['municipality']) for r in airports} | {
        normalize(r[k]) for r in snapshot['search_cities'] for k in ['city_name_fa', 'city_name_en']}
    names.discard('')
    cities = []
    with zipfile.ZipFile(a.cities) as z:
        with z.open('cities500.txt') as source:
            for line in io.TextIOWrapper(source, encoding='utf-8'):
                r = line.rstrip('\n').split('\t')
                if r[6] != 'P': continue
                aliases = [s for s in [r[1], r[2], *r[3].split(',')] if normalize(s) in names]
                if aliases:
                    cities.append(dict(id=int(r[0]), name=r[1], ascii=r[2], aliases=sorted(set(aliases)),
                        latitude=float(r[4]), longitude=float(r[5]), country=r[8], admin1=r[10],
                        feature=r[7]))
    countries = []
    for line in a.countries.read_text(encoding='utf-8').splitlines():
        if not line or line.startswith('#'): continue
        r = line.split('\t')
        countries.append(dict(code=r[0], name=r[4], geonames_id=r[16]))
    sources = [{ 'url': url, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest() }
        for path, url in [(a.airports, 'https://davidmegginson.github.io/ourairports-data/airports.csv'),
            (a.cities, 'https://download.geonames.org/export/dump/cities500.zip'),
            (a.countries, 'https://download.geonames.org/export/dump/countryInfo.txt')]]
    write_json(a.output, dict(airports=airports, cities=cities, countries=countries, sources=sources))
    print(json.dumps(dict(airports=len(airports), cities=len(cities), countries=len(countries))))


if __name__ == '__main__': main()
