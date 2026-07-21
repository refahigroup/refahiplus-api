param(
    [string]$AirportsUrl = 'https://davidmegginson.github.io/ourairports-data/airports.csv',
    [string]$CountriesUrl = 'https://davidmegginson.github.io/ourairports-data/countries.csv',
    [string]$Version = '20260714',
    [string]$OutputPath = 'src/Refahi.Modules.Flights.Infrastructure/Data/airports-20260714.json.gz'
)

$ErrorActionPreference = 'Stop'
$work = Join-Path ([System.IO.Path]::GetTempPath()) "refahi-flight-airports-$Version"
[System.IO.Directory]::CreateDirectory($work) | Out-Null
$airportsPath = Join-Path $work 'airports.csv'
$countriesPath = Join-Path $work 'countries.csv'

Invoke-WebRequest -Uri $AirportsUrl -OutFile $airportsPath -UseBasicParsing
Invoke-WebRequest -Uri $CountriesUrl -OutFile $countriesPath -UseBasicParsing

$airportRows = @(Import-Csv -LiteralPath $airportsPath | Where-Object {
    $_.iata_code.Trim().Length -eq 3 -and $_.type -ne 'closed'
})
$countryRows = Import-Csv -LiteralPath $countriesPath
$countryEnglish = @{}
$countryPersian = @{}
$wikidataAirportPersian = @{}
$wikidataCityPersian = @{}
$previousCulture = [System.Globalization.CultureInfo]::CurrentUICulture
[System.Globalization.CultureInfo]::CurrentUICulture = [System.Globalization.CultureInfo]::GetCultureInfo('fa-IR')
try {
    foreach ($country in $countryRows) {
        $countryEnglish[$country.code] = $country.name
        try {
            $countryPersian[$country.code] = ([System.Globalization.RegionInfo]::new($country.code)).DisplayName
        } catch {
            $countryPersian[$country.code] = $country.name
        }
    }
} finally {
    [System.Globalization.CultureInfo]::CurrentUICulture = $previousCulture
}

try {
    $headers = @{ 'User-Agent' = 'RefahiAirportImporter/1.0 (reference-data import)' }
    $countryQuery = @'
SELECT ?iso ?label WHERE {
  ?country wdt:P297 ?iso; rdfs:label ?label.
  FILTER(LANG(?label) = "fa")
}
'@
    $countryResult = Invoke-RestMethod -Uri 'https://query.wikidata.org/sparql' -Method Post -Headers $headers -Body @{ query=$countryQuery; format='json' }
    foreach ($binding in $countryResult.results.bindings) {
        $countryPersian[$binding.iso.value.ToUpperInvariant()] = $binding.label.value
    }

    $iataCodes = @($airportRows | ForEach-Object { $_.iata_code.Trim().ToUpperInvariant() } | Sort-Object -Unique)
    for ($offset = 0; $offset -lt $iataCodes.Count; $offset += 250) {
        $last = [Math]::Min($offset + 249, $iataCodes.Count - 1)
        $values = ($iataCodes[$offset..$last] | ForEach-Object { '"' + $_ + '"' }) -join ' '
        $airportQuery = @"
SELECT ?iata ?airportLabel ?cityLabel WHERE {
  VALUES ?iata { $values }
  ?airport wdt:P238 ?iata; rdfs:label ?airportLabel.
  FILTER(LANG(?airportLabel) = "fa")
  OPTIONAL {
    ?airport wdt:P931 ?city.
    ?city rdfs:label ?cityLabel.
    FILTER(LANG(?cityLabel) = "fa")
  }
}
"@
        $airportResult = Invoke-RestMethod -Uri 'https://query.wikidata.org/sparql' -Method Post -Headers $headers -Body @{ query=$airportQuery; format='json' }
        foreach ($binding in $airportResult.results.bindings) {
            $code = $binding.iata.value.ToUpperInvariant()
            if ($code.Length -eq 3) {
                $wikidataAirportPersian[$code] = $binding.airportLabel.value
                if ($binding.cityLabel) { $wikidataCityPersian[$code] = $binding.cityLabel.value }
            }
        }
    }
} catch {
    Write-Warning "Wikidata enrichment was unavailable: $($_.Exception.Message)"
}

$popular = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@('THR','IKA','MHD','SYZ','KIH','IFN','AWZ','TBZ','GSM','BND') | ForEach-Object { [void]$popular.Add($_) }
$cityCodes = @{ IKA = 'THR' }
$verified = @{
    THR = @('فرودگاه بین‌المللی مهرآباد','تهران')
    IKA = @('فرودگاه بین‌المللی امام خمینی','تهران')
    MHD = @('فرودگاه بین‌المللی شهید هاشمی‌نژاد','مشهد')
    SYZ = @('فرودگاه بین‌المللی شهید دستغیب','شیراز')
    KIH = @('فرودگاه بین‌المللی کیش','کیش')
    IFN = @('فرودگاه بین‌المللی شهید بهشتی','اصفهان')
    AWZ = @('فرودگاه بین‌المللی سپهبد قاسم سلیمانی','اهواز')
    TBZ = @('فرودگاه بین‌المللی شهید مدنی','تبریز')
    GSM = @('فرودگاه بین‌المللی قشم','قشم')
    BND = @('فرودگاه بین‌المللی بندرعباس','بندرعباس')
    KER = @('فرودگاه بین‌المللی آیت‌الله هاشمی رفسنجانی','کرمان')
    RAS = @('فرودگاه سردار جنگل','رشت')
}

function Convert-ToPersianTransliteration([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return 'نامشخص' }
    $text = $Value.Normalize([Text.NormalizationForm]::FormD).ToLowerInvariant()
    $builder = [Text.StringBuilder]::new()
    foreach ($char in $text.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($char) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($char)
        }
    }
    $text = $builder.ToString()
    $phrases = [ordered]@{
        'international airport'='فرودگاه بین‌المللی'; 'regional airport'='فرودگاه منطقه‌ای';
        'municipal airport'='فرودگاه شهری'; 'airport'='فرودگاه'; 'international'='بین‌المللی';
        'sh'='ش'; 'ch'='چ'; 'zh'='ژ'; 'kh'='خ'; 'gh'='ق'; 'ph'='ف'; 'th'='ت';
        'oo'='و'; 'ee'='ی'; 'a'='ا'; 'b'='ب'; 'c'='ک'; 'd'='د'; 'e'='ِ'; 'f'='ف';
        'g'='گ'; 'h'='ه'; 'i'='ی'; 'j'='ج'; 'k'='ک'; 'l'='ل'; 'm'='م'; 'n'='ن';
        'o'='و'; 'p'='پ'; 'q'='ق'; 'r'='ر'; 's'='س'; 't'='ت'; 'u'='و'; 'v'='و';
        'w'='و'; 'x'='کس'; 'y'='ی'; 'z'='ز'
    }
    foreach ($entry in $phrases.GetEnumerator()) { $text = $text.Replace($entry.Key, $entry.Value) }
    return ($text -replace 'ِ+','' -replace '\s+',' ').Trim()
}

$records = [System.Collections.Generic.List[object]]::new()
foreach ($airport in $airportRows) {
    $iata = $airport.iata_code.Trim().ToUpperInvariant()
    if ($iata.Length -ne 3 -or $airport.type -eq 'closed') { continue }

    $cityEn = if ([string]::IsNullOrWhiteSpace($airport.municipality)) { $airport.name } else { $airport.municipality }
    $translationSource = 'transliteration'
    if ($verified.ContainsKey($iata)) {
        $airportFa = $verified[$iata][0]
        $cityFa = $verified[$iata][1]
        $translationSource = 'verified'
    } elseif ($wikidataAirportPersian.ContainsKey($iata)) {
        $airportFa = $wikidataAirportPersian[$iata]
        $cityFa = if ($wikidataCityPersian.ContainsKey($iata)) { $wikidataCityPersian[$iata] } else { Convert-ToPersianTransliteration $cityEn }
        $translationSource = 'wikidata'
    } else {
        $airportFa = Convert-ToPersianTransliteration $airport.name
        $cityFa = Convert-ToPersianTransliteration $cityEn
    }

    $countryEn = if ($countryEnglish.ContainsKey($airport.iso_country)) { $countryEnglish[$airport.iso_country] } else { $airport.iso_country }
    $countryFa = if ($countryPersian.ContainsKey($airport.iso_country)) { $countryPersian[$airport.iso_country] } else { Convert-ToPersianTransliteration $countryEn }
    $cityCode = if ($cityCodes.ContainsKey($iata)) { $cityCodes[$iata] } else { $iata }

    $records.Add([ordered]@{
        iataCode=$iata; icaoCode=($(if ($airport.icao_code) { $airport.icao_code.ToUpperInvariant() } else { $null }));
        cityCode=$cityCode; airportNameFa=$airportFa; airportNameEn=$airport.name; cityNameFa=$cityFa;
        cityNameEn=$cityEn; countryCode=$airport.iso_country; countryNameFa=$countryFa; countryNameEn=$countryEn;
        latitude=($(if ($airport.latitude_deg) { [decimal]::Parse($airport.latitude_deg, [Globalization.CultureInfo]::InvariantCulture) } else { $null }));
        longitude=($(if ($airport.longitude_deg) { [decimal]::Parse($airport.longitude_deg, [Globalization.CultureInfo]::InvariantCulture) } else { $null }));
        isPopular=$popular.Contains($iata); translationSource=$translationSource
    })
}

$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
$json = $records | Sort-Object iataCode | ConvertTo-Json -Depth 3 -Compress
$file = [System.IO.File]::Create($resolvedOutput)
try {
    $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::Optimal)
    try {
        $writer = [System.IO.StreamWriter]::new($gzip, [Text.UTF8Encoding]::new($false))
        try { $writer.Write($json) } finally { $writer.Dispose() }
    } finally { $gzip.Dispose() }
} finally { $file.Dispose() }

Write-Output "Generated $($records.Count) active IATA airports at $resolvedOutput"
