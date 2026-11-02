#!/usr/bin/env bash
# Run on the confirmed production host, from the extracted release directory.
set -euo pipefail
umask 077
cd -- "$(dirname -- "$0")"
sha256sum -c SHA256SUMS

docker inspect tochal_api >/dev/null
docker inspect infra_postgres >/dev/null
docker exec infra_postgres psql -X -U refahi -d tochal-db -v ON_ERROR_STOP=1 \
  -c 'SELECT current_database(), current_user;'

# The API is in a crash loop. Stop it so startup migrations cannot race the upgrade.
docker stop tochal_api
trap 'echo "Recovery stopped. Keep the SQL output and backup; inspect the error before restarting tochal_api." >&2' ERR

backup_dir=/root/tochal-geography-backups
mkdir -p -- "$backup_dir"
backup_file=$(mktemp "$backup_dir/tochal-db-$(date -u +%Y%m%dT%H%M%SZ).dump.XXXXXX")
docker exec infra_postgres pg_dump -U refahi -d tochal-db -Fc > "$backup_file"
test -s "$backup_file"
docker exec -i infra_postgres pg_restore --list < "$backup_file" >/dev/null
echo "Backup archive created and its table of contents checked: $backup_file"
sha256sum "$backup_file"

docker cp tochal-geography-schema.sql infra_postgres:/tmp/tochal-geography-schema.sql
docker exec infra_postgres psql -X -U refahi -d tochal-db -v ON_ERROR_STOP=1 \
  -f /tmp/tochal-geography-schema.sql

docker start tochal_api
healthy=false
for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error --max-time 3 \
      https://tochalvip.ir/api/health > health.json 2>/dev/null; then
    healthy=true
    break
  fi
  sleep 2
done
docker logs --since 2m --tail 100 tochal_api
docker inspect tochal_api --format 'status={{.State.Status}} exit={{.State.ExitCode}} restarts={{.RestartCount}}'
if [ "$healthy" != true ]; then
  echo 'API health did not recover. Send the new startup logs; do not bypass another migration guard.' >&2
  exit 1
fi
cat health.json
printf '\n'
curl --fail-with-body --max-time 20 https://tochalvip.ir/api/store/modules
printf '\n'
echo 'API endpoints responded. Reload the homepage and verify actual category cards.'
