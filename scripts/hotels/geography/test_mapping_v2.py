import unittest
from build_mapping_v2 import Gazetteer, deduplicate


class ReferenceMappingV2Tests(unittest.TestCase):
    def test_merge_only_equal_city_and_province_preserves_before_image(self):
        source=[dict(Id=9,Name='مرزن آباد',ProvinceId=1,Slug='old'),
                dict(Id=3,Name='مرزن\u200cآباد',ProvinceId=1,Slug='kept'),
                dict(Id=4,Name='مرزن آباد',ProvinceId=2,Slug='another-province')]
        kept,merges=deduplicate(source)
        self.assertEqual([c['Id'] for c in kept],[3,4])
        self.assertEqual(merges,[dict(old_id=9,new_id=3,before=source[0])])
        self.assertEqual(source[0]['Slug'],'old')

    def test_country_qualifies_localized_alias_before_english_comparison(self):
        # A Persian Dubai alias can exist only on an unrelated Iraqi locality in a source.
        gaz=Gazetteer.__new__(Gazetteer)
        ae=dict(id=1,country='AE',admin='AE.03')
        iq=dict(id=2,country='IQ',admin='IQ.07')
        gaz.by_name={'دوبی':{2:iq},'dubai':{1:ae,2:iq}}
        self.assertEqual(gaz.find('دوبی','Dubai',country='AE'),[ae])
        self.assertEqual(len(gaz.find('unknown','Dubai')),2)

    def test_admin1_and_country_cannot_be_bypassed(self):
        gaz=Gazetteer.__new__(Gazetteer)
        ir=dict(id=1,country='IR',admin='IR.26')
        tr=dict(id=2,country='TR',admin='TR.34')
        gaz.by_name={'shared':{1:ir,2:tr}}
        self.assertEqual(gaz.find('shared',country='IR',province_key='IR.26'),[ir])
        self.assertEqual(gaz.find('shared',country='TR',province_key='IR.26'),[])


if __name__=='__main__':unittest.main()
