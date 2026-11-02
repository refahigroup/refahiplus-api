import unittest
import json
import tempfile
from pathlib import Path
from build_mapping_v2 import validate_decision
from research_resolutions import airport_matches_place, select_city, write_review_queue


class ResearchResolutionTests(unittest.TestCase):
    def setUp(self):
        self.record=dict(external_id='native',name='شهر',code='ABC',reference_id=None)
        self.decision=dict(expected_name='شهر',expected_code='ABC',country='IR',admin1='IR.26',
            geonames=dict(id=123,country='IR',admin1='IR.26',source_sha256='a'*64),
            evidence=[dict(url='https://source-a.example/data'),dict(url='https://source-b.example/data')],
            location_kind='city',method='two_sources',allow_inventory_province_conflict=False)

    def test_valid_review(self):
        validate_decision(self.decision,self.record,{'IR.26':{}})

    def test_stale_native_data_and_existing_mapping_are_rejected(self):
        for changed in [dict(name='changed'),dict(code='XYZ'),dict(reference_id=1)]:
            with self.subTest(changed=changed),self.assertRaises(ValueError):
                validate_decision(self.decision,{**self.record,**changed},{'IR.26':{}})

    def test_wrong_country_or_admin_is_rejected(self):
        for field,value in [('country','TR'),('admin1','IR.35')]:
            d={**self.decision,field:value}
            with self.subTest(field=field),self.assertRaises(ValueError):validate_decision(d,self.record,{'IR.26':{}})

    def test_single_source_and_arbitrary_inventory_exception_are_rejected(self):
        for change in [dict(evidence=[self.decision['evidence'][0]]),
                       dict(evidence=[dict(url='https://same.example/a'),dict(url='https://same.example/b')]),
                       dict(allow_inventory_province_conflict=True),dict(location_kind='province')]:
            with self.subTest(change=change),self.assertRaises(ValueError):
                validate_decision({**self.decision,**change},self.record,{'IR.26':{}})

    def test_homonyms_are_not_resolved_by_population(self):
        a=dict(id=1,feature_class='P',feature='PPL',admin1='IR.26',admin2='1',population=999999)
        b={**a,'id':2,'population':10,'admin2':'2'}
        self.assertIsNone(select_city([a,b]))
        self.assertEqual(select_city([a,b],'شهرستان الف',{'IR.26.1':{'شهرستانالف'},'IR.26.2':{'ب'}}),a)
        self.assertIsNone(select_city([a],'ج',{'IR.26.1':{'الف'}}))
        self.assertIsNone(select_city([{**a,'feature_class':'A','feature':'ADM2'}]))
        self.assertIsNone(select_city([{**a,'feature':'PPLQ'}]))

    def test_airport_code_without_location_agreement_is_not_enough(self):
        geo=dict(feature_class='P',feature='PPLA',names=['khorramabad','khd'],latitude=33.48778,longitude=48.35583)
        airport=dict(municipality='',name='Khoram Abad Airport',iata_code='KHD',latitude_deg='33.4354',longitude_deg='48.2829')
        self.assertTrue(airport_matches_place(airport,geo))
        self.assertFalse(airport_matches_place({**airport,'longitude_deg':'52'},geo))
        self.assertFalse(airport_matches_place({**airport,'iata_code':'XYZ'},geo))
        self.assertFalse(airport_matches_place(airport,{**geo,'feature_class':'A','feature':'ADM2'}))

    def test_review_queue_excludes_resolved_and_prioritizes_inventory(self):
        outcomes=[dict(external_id='done',status='resolved'),dict(external_id='empty',name='ب',code='B',status='exclude_invalid_record'),
                  dict(external_id='hotels',name='الف',code='A',status='needs_provider_or_more_evidence')]
        baseline=dict(cities=[dict(provider='Lamasoo',external_id='empty',evidence={}),
                            dict(provider='Lamasoo',external_id='hotels',evidence=dict(inventory=[dict(hotel_id='h')]))])
        proposals=dict(records=[dict(external_id=k,status='previous',candidates=[]) for k in ['empty','hotels']])
        with tempfile.TemporaryDirectory() as folder:
            write_review_queue(Path(folder),outcomes,baseline,proposals)
            result=json.loads((Path(folder)/'provider-review.json').read_text(encoding='utf-8'))['records']
            self.assertEqual([r['external_id'] for r in result],['hotels','empty'])
            self.assertTrue(all(not r['provider_confirmation_received'] and r['requested_information'] for r in result))


if __name__=='__main__':unittest.main()
