$ErrorActionPreference = 'Stop'
# Only the dedicated local test database is targeted; no production connection is accepted.
$samples = @(
    'https://store.snapptrip.com/domestic-hotel/3233/1b3d9c81-59d3-4ab6-9de8-a1a9402d1c34.jpeg',
    'https://store.snapptrip.com/domestic-hotel/galleries/hotel/2920/52092.jpeg'
)
foreach ($sample in $samples) {
    $uri = [Uri]$sample
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($uri.AbsoluteUri))).ToLowerInvariant()
    $id = [Guid]::NewGuid()
    $sql = @"
INSERT INTO media.external_sources ("Id","SourceUrl","FileName","CreatedAt","StateChangedAt","Status")
VALUES ('$id','$sample','$hash',now(),now(),0) ON CONFLICT ("FileName") DO NOTHING;
INSERT INTO media.external_source_origins ("SourceId","Module","Provider")
SELECT "Id",'LocalMediaTest','SnappTrip' FROM media.external_sources WHERE "FileName"='$hash' ON CONFLICT DO NOTHING;
SELECT 'https://localhost:5001/media/external/' || replace("Id"::text,'-','') FROM media.external_sources WHERE "FileName"='$hash';
"@
    $sql | docker exec -i postgres psql -U postgres -qAt -v ON_ERROR_STOP=1 -d refahi-media-local
    if ($LASTEXITCODE -ne 0) { throw 'Could not seed local sample media. Start the local API and apply its Media migration first.' }
}
