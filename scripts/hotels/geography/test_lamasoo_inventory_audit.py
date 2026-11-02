import unittest
from check_lamasoo_inventory import hotels_for_city


class InventoryAuditTests(unittest.TestCase):
    def test_empty_success_is_distinct_from_invalid_response(self):
        self.assertEqual(hotels_for_city(dict(hotels=[]),'city'),[])
        for payload in [None,{},dict(error='unauthorized'),dict(hotels=None)]:
            with self.subTest(payload=payload),self.assertRaises(ValueError):hotels_for_city(payload,'city')

    def test_ignored_city_filter_is_rejected(self):
        with self.assertRaisesRegex(ValueError,'city_filter_mismatch'):
            hotels_for_city(dict(hotels=[dict(id='hotel',city='other')]),'city')

    def test_duplicate_or_missing_hotel_identity_is_rejected(self):
        hotel=dict(id='hotel',city='city')
        for rows in [[hotel,hotel],[dict(city='city')]]:
            with self.subTest(rows=rows),self.assertRaises(ValueError):hotels_for_city(dict(hotels=rows),'city')

    def test_valid_filtered_hotels_are_preserved(self):
        hotel=dict(id='hotel',name='هتل',city='city',province=None)
        self.assertEqual(hotels_for_city(dict(hotels=[hotel]),'city'),[hotel])


if __name__=='__main__':unittest.main()
