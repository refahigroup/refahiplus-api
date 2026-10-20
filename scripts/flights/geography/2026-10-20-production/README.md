# بسته مستقل جغرافیای Flights برای پروداکشن — ۲۰ اکتبر ۲۰۲۶

این بسته بر اساس فایل `flight-geography-production.json` ارسالی تهیه شده است.
نسخه انتشار **`flight-geography-20261020-production`** است. بسته قبلی لوکال
و داده خام پروداکشن تغییر نمی‌کنند. اطلاعات دقیق نسخه، هش و پوشش در
`deployment.json` و `validation.json` قرار دارد.

## علت توقف بسته قبلی

پروداکشن و لوکال هر دو ۹٬۰۶۱ فرودگاه، ۶۳ شهر تجاری و ۱۲۱ عضویت دارند.
کشورها، استان‌ها، شهرهای References و عضویت‌های کاتالوگ کاملاً یکسان‌اند؛
اما زمان واردسازی تمام فرودگاه‌ها و تعدادی از نام‌ها، کد شهرها و وضعیت فعال‌بودن
متفاوت است. جزئیات بدون اطلاعات کاربری در `snapshot-diff.json` ثبت شده‌اند.

مقدار خام YXU در پروداکشن `LON/GB` است. نگاشت واقعی آن طبق شواهد OurAirports
و GeoNames به شهر London در کانادا متصل می‌شود؛ کد تجاری LON و مقدار خام
Airports بازنویسی نمی‌شوند. پنج فرودگاه عضو کاتالوگ نیز در پروداکشن فعال‌اند
که در ورودی لوکال غیرفعال بودند؛ وضعیت پروداکشن حفظ می‌شود.

کنترل Snapshot همچنان تمام فیلدها را بررسی می‌کند. فقط نمایش منطقه زمانی
`imported_at_utc` با مقایسه `timestamptz` به همان لحظه زمانی تبدیل می‌شود؛
تغییر واقعی زمان واردسازی، حتی یک ثانیه، همچنان باعث توقف و rollback می‌شود.

## اجرای روی سرور

پیش‌نیاز: بکاپ، References V4 و Migrationهای قبلی Flights. اجرای ساختار قبلی
شما با COMMIT تمام شده است؛ بنابراین برای این سرور نیازی به تکرار مرحله ۱ نیست.
فایل `01-schema.sql` همان نسخه قبلی و قابل اجرای مجدد است.

ZIP را روی سرور استخراج کنید؛ پوشه حاصل باید `/root/flight-geography-production`
باشد. سپس:

```bash
docker cp /root/flight-geography-production infra_postgres:/tmp/
docker exec infra_postgres psql -X -U refahi -d refahidb -v ON_ERROR_STOP=1 -f /tmp/flight-geography-production/02-data.sql
```

فقط پس از موفقیت و مشاهده `COMMIT`:

```bash
docker exec infra_postgres psql -X -U refahi -d refahidb -v ON_ERROR_STOP=1 -f /tmp/flight-geography-production/03-verify.sql
```

خروجی باید نسخه `flight-geography-20261020-production` و هش درج‌شده در
`deployment.json` را نشان دهد. Verify علاوه بر گزارش، نسخه/هش و تعداد
نگاشت‌های همین انتشار را کنترل می‌کند. در صورت هر خطا، اجرای مرحله بعد را
متوقف کنید؛ کنترل‌های تعارض را حذف نکنید. تغییر داده‌ها پس از زمان Snapshot
ممکن است به خروجی‌گیری و بازتولید جدید نیاز داشته باشد.

اجرا با نقش مدیریتی `refahi` است، چون نقش قبلی روی جدول‌های References
اجازه قفل‌کردن نداشت. دسترسی runtime API جداست: اگر API با `refahi-usr`
متصل می‌شود، پیش از انتشار API جدید از داشتن USAGE روی schemaهای `references`
و `flights` و SELECT روی سه جدول جغرافیای References و دو جدول نگاشت Flights
مطمئن شوید؛ برای اجرای migration، نقش runtime را superuser نکنید.

## نتیجه مورد انتظار و بازگشت

- فرودگاه‌ها: ۹٬۰۶۱ کل، ۵٬۶۸۹ نگاشت مستند، ۳٬۳۷۲ مورد در صف بررسی.
- شهر تجاری: ۶۳ کل، ۵۷ نگاشت مستند، ۶ مورد در صف بررسی.
- فرودگاه عضو کاتالوگ: ۱۰۵؛ دارای نگاشت واقعی: ۷۰؛ عضویت‌ها: ۱۲۱.
- داده مرجع جدید: ۵٬۳۱۴ شهر، ۲۱۰ کشور و ۲۱۰ استان واسط خارجی.

نام‌های تأییدشده فارسی موجود حفظ می‌شوند. برای داده جدیدی که ترجمه فارسی
تأییدشده ندارد، نام منبع معتبر استفاده می‌شود. سایر مصرف‌کنندگان مرجع مشترک
نیز شهرهای فعال جدید را خواهند دید.

`04-rollback-data.sql` فقط نگاشت‌های همین نسخه production را با کنترل تغییرات
بعدی برمی‌گرداند؛ داده مرجع مشترک جدید و ساختار افزایشی حفظ می‌شوند. آن را
فقط در صورت تصمیم به rollback نرم‌افزار اجرا کنید، نه بعد از موفقیت Verify.

## بازتولید در مخزن توسعه

Python برای اجرای SQL روی سرور لازم نیست. برای تولید مجدد از ورودی ثبت‌شده:

```powershell
python -X utf8 scripts/flights/geography/build_mapping.py --snapshot scripts/flights/geography/2026-10-20-production/inputs/geography.json.gz --version flight-geography-20261020-production --template scripts/flights/geography/2026-10-20-production/import-template.sql --output scripts/flights/geography/.local/rebuilt-production
python -X utf8 scripts/flights/geography/verify_mapping.py --release scripts/flights/geography/2026-10-20-production
```

شواهد عمومی همان فایل نسخه‌گذاری‌شده `2026-10-20/inputs/evidence.json.gz` است.
این بسته به پروداکشن متصل نشده و فقط روی دیتابیس موقت PostgreSQL آزمایش می‌شود.
