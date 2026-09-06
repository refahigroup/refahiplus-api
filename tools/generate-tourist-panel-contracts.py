"""Generate wire DTOs from the reviewed, self-contained Tourist Panel contract.

Usage: python tools/generate-tourist-panel-contracts.py <contract.md>
The generated file is committed; this is never run during application startup.
"""
import re
import sys
from pathlib import Path

source = Path(sys.argv[1]).read_text(encoding="utf-8-sig")
blocks = re.split(r'<a id="model-(\d+)"></a>', source)
models = {}
for i in range(1, len(blocks), 2):
    number, body = int(blocks[i]), blocks[i + 1]
    name = re.search(r'### مدل \d+ — (.+)', body).group(1).strip()
    if number == 43:
        name = 'DiscountValidationResult'
    models[number] = (name, body)

def ctype(raw):
    raw = raw.replace('&lt;', '<').replace('&gt;', '>')
    if raw.startswith('array<'):
        return 'List<' + ctype(raw[6:-1]) + '>'
    match = re.search(r'#model-(\d+)', raw)
    if match:
        return 'Tp' + models[int(match[1])][0]
    if raw.startswith('string'): return 'string'
    if raw.startswith('boolean'): return 'bool'
    if raw.startswith('integer'): return 'long' if 'int64' in raw else 'int'
    if raw.startswith('number'): return 'decimal'
    return 'JsonElement'

out = ['// Generated from TouristPanel-Production-Implementation.md, API 3.7.35.',
       '// Dates and UUIDs remain wire strings; domain mapping validates their semantics.',
       'using System.Text.Json;', 'using System.Text.Json.Serialization;',
       'namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;', '']
for number, (name, body) in models.items():
    enum_values = re.search(r'مقادیر enum: `\[([^]]+)\]`', body)
    if enum_values:
        pairs = re.findall(r'(-?\d+) - ([A-Za-z_][A-Za-z_0-9]*)', body)
        out += ['public enum Tp' + name, '{']
        out += ['    ' + key + ' = ' + value + ',' for value, key in pairs]
        out += ['}', '']
        continue
    out += ['public sealed record Tp' + name, '{']
    for line in body.splitlines():
        if not line.startswith('| '): continue
        cells = [c.strip() for c in line.strip('|').split('|')]
        if len(cells) != 6 or not re.fullmatch('[A-Za-z][A-Za-z0-9]*', cells[0]): continue
        wire, raw, _, nullable, _, _ = cells
        typ = ctype(raw)
        is_ref = typ == 'string' or typ.startswith('List<') or (typ.startswith('Tp') and not 'مقادیر enum:' in models[int(re.search(r'#model-(\d+)', raw)[1])][1])
        optional = nullable == 'بله' or is_ref
        prop = wire[0].upper() + wire[1:]
        out += ['    [JsonPropertyName("' + wire + '")]',
                '    public ' + typ + ('?' if optional else '') + ' ' + prop + ' { get; init; }']
    out += ['}', '']
dest = Path('src/Refahi.Modules.Commerce.Infrastructure/Providers/TouristPanel/Contracts/TouristPanelContracts.cs')
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text('\n'.join(out), encoding='utf-8')
print(f'Generated {len(models)} models in {dest}')
