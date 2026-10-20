"""Export geography only in a single consistent read-only transaction; never exports application secrets."""
import argparse
import gzip
import hashlib
import json
import subprocess
from pathlib import Path

SNAPSHOT_SQL = '''BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object(
 'airports',(SELECT jsonb_agg(to_jsonb(a) ORDER BY iata_code) FROM flights.airports a),
 'search_cities',(SELECT jsonb_agg(to_jsonb(c) ORDER BY city_code) FROM flights.search_cities c),
 'search_memberships',(SELECT jsonb_agg(to_jsonb(m) ORDER BY is_domestic,city_code,airport_code) FROM flights.search_memberships m),
 'cities',(SELECT jsonb_agg(to_jsonb(c) ORDER BY "Id") FROM "references".cities c),
 'provinces',(SELECT jsonb_agg(to_jsonb(p) ORDER BY "Id") FROM "references".provinces p),
 'countries',(SELECT jsonb_agg(to_jsonb(c) ORDER BY "Code") FROM "references".countries c),
 'v4_installed',to_regclass('"references".province_reorganization_history') IS NOT NULL);
COMMIT;'''


def write_json(path, data):
    raw = (json.dumps(data, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n').encode('utf-8')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(gzip.compress(raw, mtime=0) if path.suffix == '.gz' else raw)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--docker-container', required=True)
    parser.add_argument('--database', required=True)
    parser.add_argument('--user', default='postgres')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = subprocess.run(['docker', 'exec', '-i', args.docker_container, 'psql', '-X', '-U', args.user,
        '-d', args.database, '-Atq', '-v', 'ON_ERROR_STOP=1'], input=SNAPSHOT_SQL,
        capture_output=True, text=True, encoding='utf-8', check=True)
    data = json.loads(result.stdout)
    if not data['v4_installed']:
        raise ValueError('Reference geography V4 is required')
    write_json(args.output, data)
    print(json.dumps({'sha256': hashlib.sha256(args.output.read_bytes()).hexdigest(),
        'counts': {k: len(v) for k, v in data.items() if isinstance(v, list)}}, sort_keys=True))


if __name__ == '__main__':
    main()
