import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from build_mapping import build, normalize, read_reference_rows


class GeographyMappingTests(unittest.TestCase):
    def test_normalization_does_not_merge_different_letters(self):
        self.assertEqual(normalize('بندر انزلي'), normalize('بندرانزلی'))
        self.assertEqual(normalize('مرزن\u200cآباد'), normalize('مرزن آباد'))
        self.assertNotEqual(normalize('آمل'), normalize('امل'))

    def test_dump_is_parsed_as_data_without_executing_other_statements(self):
        text = '''DROP DATABASE production;
INSERT INTO "references".cities ("Id", "Name", "ProvinceId", "IsActive") VALUES (1, 'O''Brien', 2, true);
'''
        self.assertEqual(read_reference_rows(text, 'cities'),
                         [dict(Id=1, Name="O'Brien", ProvinceId=2, IsActive=True)])

    def test_country_province_and_duplicate_guards(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            dump = root / 'ref.sql'
            dump.write_text('''
INSERT INTO "references".provinces ("Id", "Name", "IsActive") VALUES (1, 'تهران', true);
INSERT INTO "references".provinces ("Id", "Name", "IsActive") VALUES (2, 'گیلان', true);
INSERT INTO "references".cities ("Id", "Name", "ProvinceId", "IsActive") VALUES (1, 'تهران', 1, true);
INSERT INTO "references".cities ("Id", "Name", "ProvinceId", "IsActive") VALUES (2, 'آستارا', 2, true);
INSERT INTO "references".cities ("Id", "Name", "ProvinceId", "IsActive") VALUES (3, 'تکراری', 1, true);
INSERT INTO "references".cities ("Id", "Name", "ProvinceId", "IsActive") VALUES (4, 'تکراری', 1, true);
''', encoding='utf-8')
            def save(name, obj): (root / name).write_text(json.dumps(obj, ensure_ascii=False), encoding='utf-8')
            save('snapptrip-cities.raw.json', [dict(id=11,title_fa='تهران',state=dict(id=100,name='unused',title='تهران')),
                                              dict(id=12,title_fa='تکراری',state=dict(id=100,title='تهران'))])
            save('snapptrip-provinces.json', [dict(id=100,title='تهران',city_ids=[11,12])])
            save('lamasoo-cities.raw.json', {'cities':[dict(id='a',name='تهران',code='THR'),
                 dict(id='b',name='آستارا',code='AST'),dict(id='c',name='تکراری',code='DUP'),
                 dict(id='d',name='تهران',code='OTHER'),dict(id='e',name='تهران',code='THR')]})
            save('lamasoo-inventory.raw.json', {'hotels':[
                 dict(id='h1',city='a',province='تهران',country='ایران'),
                 dict(id='h2',city='b',province='تهران',country='ایران'),
                 dict(id='h3',city='b',province='گیلان',country='ایران'),
                 dict(id='h4',city='c',province='تهران',country='ایران')]})
            with contextlib.redirect_stdout(io.StringIO()): build(dump, root, root / 'out')
            r=json.loads((root/'out/mapping-report.json').read_text(encoding='utf-8'))
            cities={(x['provider'],x['external_id']):x for x in r['cities']}
            self.assertEqual(cities['Lamasoo','a']['reference_id'],1)
            self.assertEqual(cities['Lamasoo','b']['reason'],'conflicting_provider_provinces')
            self.assertEqual(cities['Lamasoo','c']['reason'],'ambiguous_reference_city')
            self.assertEqual(cities['Lamasoo','d']['reason'],'missing_country_evidence')
            self.assertEqual(cities['Lamasoo','e']['reference_id'],1)
            self.assertEqual(cities['SnappTrip','12']['reason'],'ambiguous_reference_city')


if __name__ == '__main__': unittest.main()
