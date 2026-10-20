import unittest
import contextlib
import hashlib
import io
import json
import tempfile
from pathlib import Path
from build_mapping import read_reference_rows
from build_mapping_v2 import deduplicate
from build_mapping_v4 import prepare, main


class ConfirmedMappingTests(unittest.TestCase):
    def test_checkout_inputs_reproduce_the_published_sql_bytes(self):
        root=Path(__file__).parent
        expected=json.loads((root/'2026-10-19-v4/validation.json').read_text(encoding='utf-8'))['files']
        with tempfile.TemporaryDirectory() as folder,contextlib.redirect_stdout(io.StringIO()):
            main(output=Path(folder))
            for item in expected:
                data=(Path(folder)/item['file']).read_bytes()
                self.assertEqual(hashlib.sha256(data).hexdigest(),item['sha256'])
                self.assertEqual(data,(root/'2026-10-19-v4'/item['file']).read_bytes())

    def test_bundled_fixture_contains_only_reference_inserts_and_original_duplicates(self):
        source=(Path(__file__).parent/'fixtures/reference-geography.sql').read_text(encoding='utf-8')
        cities=read_reference_rows(source,'cities');provinces=read_reference_rows(source,'provinces')
        self.assertEqual((len(cities),len(provinces)),(1319,31))
        self.assertEqual(len(deduplicate(cities)[1]),4)
        self.assertTrue(all(line.startswith('--') or line.startswith('INSERT INTO "references".cities ') or
            line.startswith('INSERT INTO "references".provinces ') for line in source.splitlines() if line))

    def setUp(self):
        self.source=dict(cities=[dict(provider='Lamasoo',external_id='native',reference_id=None,name='نام خام',code='ABC',reason='missing')])
        self.decisions=dict(authority='explicit user',confirmed_at='2026-10-19',records=[dict(city_id='native',native_name='نام خام',name='شهر',
            country='TR',reference_city_id=10,hotel_id='hotel',enabled=True)])
        self.cities=[dict(Id=10,Name='شهر',ProvinceId=33)]
        self.provinces=[dict(Id=1,CountryCode='IR'),dict(Id=32,CountryCode='TR'),dict(Id=33,CountryCode='TR')]
        self.audit=dict(records=[dict(city_id='native',checked_at_utc='2026-10-19',hotels=[dict(id='hotel',address='original')])])

    def prepare(self):return prepare(self.source,self.decisions,self.cities,self.provinces,self.audit)

    def test_country_bucket_reuses_lowest_existing_id_and_keeps_city_id(self):
        buckets,merges,rows,added=self.prepare()
        self.assertEqual(buckets,{'TR':32});self.assertEqual(merges,{32:32,33:32})
        self.assertEqual(rows[0]['city_id'],10);self.assertEqual(rows[0]['province_id'],32)
        self.assertEqual(rows[0]['evidence']['raw_inventory_hotels'][0]['address'],'original')
        self.assertEqual(added,[]);self.assertEqual(self.cities[0]['ProvinceId'],33)

    def test_stale_name_already_mapped_or_wrong_hotel_is_rejected(self):
        for field,value in [('name','changed'),('reference_id',123)]:
            original=self.source['cities'][0][field];self.source['cities'][0][field]=value
            with self.subTest(field=field),self.assertRaises(ValueError):self.prepare()
            self.source['cities'][0][field]=original
        self.decisions['records'][0]['hotel_id']='other'
        with self.assertRaises(ValueError):self.prepare()

    def test_wrong_canonical_province_is_rejected(self):
        self.cities[0]['ProvinceId']=1
        with self.assertRaises(ValueError):self.prepare()

    def test_disabled_native_caption_cannot_be_enabled(self):
        self.source['cities'][0]['name']='غیرفعال';self.decisions['records'][0]['native_name']='غیرفعال'
        with self.assertRaises(ValueError):self.prepare()
        self.decisions['records'][0]['enabled']=False
        self.assertFalse(self.prepare()[2][0]['enabled'])

    def test_duplicate_user_decision_is_rejected(self):
        self.decisions['records'].append(dict(self.decisions['records'][0]))
        with self.assertRaises(ValueError):self.prepare()


if __name__=='__main__':unittest.main()
