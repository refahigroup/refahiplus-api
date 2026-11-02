"""Exercise the shipped Nginx snippets against a local disposable Docker proxy."""
import concurrent.futures
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import uuid

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('render', ROOT/'tools/render-catalogue-nginx.py')
render = importlib.util.module_from_spec(spec)
spec.loader.exec_module(render)


def main():
    name = 'codex-catalogue-nginx-'+uuid.uuid4().hex[:10]
    def docker(*args, **kwargs):
        return subprocess.run(['docker', *args], capture_output=True, text=True, encoding='utf-8', timeout=120, check=True, **kwargs).stdout.strip()
    created = False
    try:
        with tempfile.TemporaryDirectory(prefix='catalogue-nginx-') as directory:
            directory = Path(directory)
            (directory/'http.conf').write_text(render.render('127.0.0.1'), encoding='utf-8')
            for source in (ROOT/'deployment/nginx').glob('*.conf'):
                (directory/source.name).write_bytes(source.read_bytes())
            (directory/'nginx.conf').write_text('''events {}\nhttp {
include /qa/http.conf;
server { listen 8081; return 200 '{"success":true,"data":{}}'; }
server { listen 8080; access_log off;
include /qa/catalogue-errors.conf;
location /api/catalogue/ { include /qa/catalogue-api-location.conf; proxy_pass http://127.0.0.1:8081; }
location = /catalogue { include /qa/catalogue-page-location.conf; proxy_pass http://127.0.0.1:8081; }
}}''', encoding='utf-8')
            docker('run', '-d', '--name', name, '-p', '127.0.0.1::8080', '-v', str(directory)+':/qa:ro',
                   'nginx:1.28-alpine', 'nginx', '-c', '/qa/nginx.conf', '-g', 'daemon off;')
            created = True
            docker('exec', name, 'nginx', '-t', '-c', '/qa/nginx.conf')
            port = docker('port', name, '8080/tcp').split(':')[-1]
            url = f'http://127.0.0.1:{port}'
            def get(index):
                request = Request(url+'/api/catalogue/items?q=PRIVATE_QUERY&latitude=PRIVATE_COORD', headers={'X-Forwarded-For': '127.0.0.1'})
                try:
                    with urlopen(request, timeout=10) as response: return response.status
                except HTTPError as error:
                    assert error.code == 429
                    assert error.headers['Retry-After'] == '1'
                    assert json.load(error)['statusCode'] == 429
                    return 429
            with concurrent.futures.ThreadPoolExecutor(max_workers=30) as pool: statuses = list(pool.map(get, range(160)))
            assert 200 in statuses and 429 in statuses, statuses
            # Both public routes use the SAME bucket.
            try:
                urlopen(url+'/catalogue', timeout=10)
                raise AssertionError('SSR route escaped the shared exhausted bucket')
            except HTTPError as error: assert error.code == 429
            # The trusted Web peer bypasses only the per-IP API bucket.
            docker('exec', name, 'sh', '-c', 'i=0; while [ "$i" -lt 100 ]; do wget -qO /dev/null http://127.0.0.1:8080/api/catalogue/items || exit 1; i=$((i+1)); done')
            logs = docker('exec', name, 'cat', '/var/log/nginx/catalogue.json')
            assert 'PRIVATE_QUERY' not in logs and 'PRIVATE_COORD' not in logs
            assert '"reason":"rate"' in logs
            result = {'ready': True, 'public200': statuses.count(200), 'public429': statuses.count(429), 'trustedSSRApiCalls': 100, 'queriesLogged': False}
            output = ROOT/'artifacts/catalogue-release/nginx-test.json'; output.parent.mkdir(parents=True, exist_ok=True)
            output.write_text(json.dumps(result, indent=2), encoding='utf-8'); print(json.dumps(result))
    except subprocess.CalledProcessError as error:
        print(error.stderr)
        if created: print(docker('exec', name, 'nginx', '-T', '-c', '/qa/nginx.conf'))
        raise
    finally:
        if created: docker('rm', '-f', name)


if __name__ == '__main__': main()
