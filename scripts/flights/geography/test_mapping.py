import unittest
from build_mapping import build, choose_actual, choose_commercial, distance
from prepare_evidence import normalize


class MappingTests(unittest.TestCase):
    def setUp(self):
        self.geo = dict(id=1,name='London',ascii='London',aliases=['London'],country='CA',admin1='08',
            latitude=43.0,longitude=-81.2,feature='PPL')
        self.airport = dict(iata_code='YXU',icao_code='CYXU',city_name_en='London',city_name_fa='لندن',
            country_code='CA',latitude=43.03,longitude=-81.15,is_active=True)
        self.evidence = dict(id='100',iata_code='YXU',icao_code='CYXU',iso_country='CA',municipality='London',
            latitude_deg='43.03',longitude_deg='-81.15',type='large_airport')

    def test_arabic_persian_and_diacritics_normalize(self):
        self.assertEqual(normalize('كِيش'),normalize('کیش'))
        self.assertEqual(normalize('São Paulo'),normalize('Sao Paulo'))

    def test_exact_municipality_country_and_position(self):
        geo,reason,_ = choose_actual(self.airport,[self.evidence],{('CA','london'):[self.geo]})
        self.assertEqual(geo,self.geo)
        self.assertIsNone(reason)

    def test_nearest_city_without_name_is_not_a_mapping(self):
        geo,reason,_=choose_actual(self.airport,[self.evidence],{('CA','nearby'):[self.geo]})
        self.assertIsNone(geo)
        self.assertIn('municipality',reason)

    def test_homonymous_cities_are_not_resolved_by_nearest(self):
        geo,reason,_=choose_actual(self.airport,[self.evidence],{('CA','london'):[self.geo,dict(self.geo,id=2)]})
        self.assertIsNone(geo)
        self.assertIn('ambiguous',reason)

    def test_reused_iata_cannot_override_snapshot_identity(self):
        other = dict(self.evidence,icao_code='OTHER',iso_country='GB')
        geo,reason,_=choose_actual(self.airport,[other],{('GB','london'):[dict(self.geo,country='GB')]})
        self.assertIsNone(geo)
        self.assertIn('identity_conflict',reason)

    def test_position_conflict_is_queued(self):
        geo,reason,_=choose_actual(dict(self.airport,latitude=0),[self.evidence],{('CA','london'):[self.geo]})
        self.assertIsNone(geo)
        self.assertEqual('airport_position_identity_conflict',reason)

    def test_duplicate_airport_source_is_queued(self):
        self.assertIsNone(choose_actual(self.airport,[self.evidence,self.evidence],{})[0])

    def test_actual_and_commercial_identity_remain_separate(self):
        gb=dict(self.geo,id=2,country='GB',latitude=51.5,longitude=0,admin1='ENG')
        snapshot=dict(airports=[self.airport],search_cities=[dict(city_code='LON',city_name_en='London',city_name_fa='لندن',country_code='GB')],
            countries=[dict(Code='CA',Name='کانادا',NameEn='Canada',IsActive=True),dict(Code='GB',Name='بریتانیا',NameEn='UK',IsActive=True)],
            provinces=[dict(Id=1,CountryCode='CA',IsSynthetic=True,IsActive=True),dict(Id=2,CountryCode='GB',IsSynthetic=True,IsActive=True)],
            cities=[dict(Id=1,Name='لندن',NameEn='London',ProvinceId=1,IsActive=True),dict(Id=2,Name='لندن',NameEn='London',ProvinceId=2,IsActive=True)])
        result=build(snapshot,dict(airports=[self.evidence],cities=[self.geo,gb],countries=[],sources=[]),'2026-10-20')
        self.assertEqual(1,result['airports'][0]['city_id'])
        self.assertEqual(2,result['commercial'][0]['city_id'])
        production=build(snapshot,dict(airports=[self.evidence],cities=[self.geo,gb],countries=[],sources=[]),
            '2026-10-20','flight-geography-20261020-production')
        self.assertEqual('flight-geography-20261020-production',production['version'])
        self.assertTrue(all(m['source_version']==production['version'] for m in production['airports']+production['commercial']))
        self.assertEqual([1],[m['city_id'] for m in production['airports']])
        self.assertEqual([2],[m['city_id'] for m in production['commercial']])

    def test_commercial_primary_name_disambiguates_alternate_name(self):
        alias=dict(self.geo,id=2,name='London District',ascii='London District')
        city=dict(city_name_en='London',city_name_fa='لندن',country_code='CA')
        self.assertEqual(1,choose_commercial(city,{('CA','london'):[self.geo,alias]})['id'])

    def test_conflicting_bilingual_identities_are_queued(self):
        city=dict(city_name_en='London',city_name_fa='لندن',country_code='CA')
        self.assertIsNone(choose_commercial(city,{('CA','london'):[self.geo],('CA',normalize('لندن')):[dict(self.geo,id=2)]}))

    def test_iran_requires_real_matching_province(self):
        snapshot=dict(airports=[dict(self.airport,country_code='IR')],search_cities=[],
            countries=[dict(Code='IR',Name='ایران',NameEn='Iran',IsActive=True)],
            provinces=[dict(Id=1,CountryCode='IR',IsSynthetic=False,IsActive=True,GeoNamesAdmin1Code='IR.01')],
            cities=[dict(Id=1,Name='London',NameEn='London',ProvinceId=1,IsActive=True)])
        result=build(snapshot,dict(airports=[dict(self.evidence,iso_country='IR')],
            cities=[dict(self.geo,country='IR',admin1='02')],countries=[],sources=[]),'2026-10-20')
        self.assertEqual('unknown_iran_province',result['remaining'][0]['reason'])
        self.assertFalse(result['additions']['provinces'])


if __name__=='__main__': unittest.main()
