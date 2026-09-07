# اجرای کاتالوگ جستجوی پرواز — 2026-09-06

این بسته جستجوی داخلی/خارجی را از داده تاریخی فرودگاه‌ها جدا می‌کند. فایل‌های SQL مستقل از Desktop هستند. منبع و نگاشت پالایش در `src/Refahi.Modules.Flights.Infrastructure/Data/search-catalog-20260906` نسخه‌گذاری شده‌اند.

## ترتیب اجرا

1. نمونه‌های قدیمی API را متوقف کنید؛ Seeder قدیمی می‌تواند نام‌ها و وضعیت فرودگاه‌ها را بازنویسی کند.
2. مطمئن شوید Migration قبلی Flights با شناسه `20260721052246_FlightAirportsReferenceData` قبلاً اعمال شده است. این بسته upgrade است؛ ایجاد دیتابیس خالی با Migrationهای کامل انجام می‌شود.
3. `01-schema.sql` را با توقف روی اولین خطا اجرا کنید. این فایل از EF تولید شده، تکرارپذیر است و تاریخچه `flights.__EFMigrationsHistory` را نیز ثبت می‌کند.
4. `02-data.sql` را اجرا کنید. اسکریپت تراکنش و advisory lock خودش را دارد؛ آن را داخل تراکنش دیگری قرار ندهید.
5. `03-verify.sql` را اجرا و خروجی را نگه دارید.
6. بک‌اند جدید و سپس فرانت‌اند جدید را منتشر کنید. Seeder همان `02-data.sql` تعبیه‌شده را اجرا می‌کند و در صورت وجود نسخه واردسازی، تغییر مجدد نمی‌دهد.

نمونه دستورها، با اتصال انتخاب‌شده توسط اپراتور و رمز در محیط امن:

```powershell
psql "$env:REFAHI_DB_CONNECTION" -v ON_ERROR_STOP=1 -f 01-schema.sql
psql "$env:REFAHI_DB_CONNECTION" -v ON_ERROR_STOP=1 -f 02-data.sql
psql "$env:REFAHI_DB_CONNECTION" -v ON_ERROR_STOP=1 -f 03-verify.sql
```

این تغییرات روی دیتابیس اصلی اجرا نشده‌اند.

## نتیجه مورد انتظار و سیاست داده

- ورودی خام: داخلی ۴۰ شهر/۴۰ زیرمجموعه، خارجی ۴۰ شهر/۹۹ زیرمجموعه.
- خروجی پالایش: ۶۳ شهر یکتا، ۱۰۵ فرودگاه یکتا، ۱۲۱ عضویت؛ داخلی ۴۰ و خارجی ۸۱ عضویت. در هر حالت ۴۰ شهر قابل انتخاب است.
- کدهای تجمیعی شهر، ایستگاه قطار/اتوبوس و مناطق آزاد غیر‌فرودگاهی از شمارش حذف شده‌اند. سایر مکان‌های هوانوردی فایل، از جمله پایگاه هواپیمای آب‌نشین و فرودگاه نظامی، حفظ شده‌اند؛ این واردسازی تأیید فعال‌بودن تجاری آن‌ها نیست.
- مشخصات هویتی ارائه‌شده در JSON اولویت دارند، از جمله انتساب `YXU` به `LON/GB`، مطابق تصمیم محصول.
- فارسی معتبر JSON، سپس فارسی معتبر دیتابیس، سپس فارسی snapshot موجود برای نصب خالی، و در نهایت انگلیسی استفاده می‌شود. نام لاتین در `faName` فارسی محسوب نمی‌شود.
- ICAO و مختصات موجود حفظ می‌شوند. رکوردهای خارج از فهرست حذف یا غیرفعال نمی‌شوند، اما در endpoint جدید قابل انتخاب نیستند.
- `source-audit.md` پالایش و اختلاف با snapshot قبلی را مستند می‌کند. `03-verify.sql` اختلاف واقعی قبل/بعد این دیتابیس را گزارش می‌کند.

## بازگردانی

API جدید را متوقف کنید و `04-rollback-data.sql` را با `ON_ERROR_STOP=1` اجرا کنید. تصویر قبل از تغییر در `flights.catalog_backups` نگه داشته شده است. داده و عضویت‌های قبلی بازگردانده می‌شوند و نسخه واردسازی حذف می‌شود. اسکریپت در برابر وجود واردسازی جدیدتر یا ممانعت FK به‌صورت تراکنشی متوقف می‌شود.

پس از rollback، اجرای مجدد API جدید کاتالوگ را دوباره وارد خواهد کرد. برای برگشت کامل نرم‌افزار، نسخه قبلی را منتشر کنید. schema افزوده‌شده می‌تواند باقی بماند؛ اگر حذف ساختار لازم بود، تنها پس از بازگردانی داده از EF برای برگشت به Migration قبلی استفاده کنید تا backup پیش از مصرف حذف نشود.

## API و URL

`GET /api/flights/locations?isDomestic=false&q=SAW&limit=20`

پاسخ در envelope استاندارد و در `data.locations` است. هر گزینه شامل `code`, `type`, `cityCode`, `cityNameFa`, `cityNameEn`, `countryCode`, `countryNameFa`, `countryNameEn`, `airportCount`, `airportNameFa`, `airportNameEn` است. برای شهر چندفرودگاهی نام فرودگاه null و نوع `City` است. `isDomestic` الزامی، `limit` بین ۱ و ۵۰ و `q` حداکثر ۲۰۰ نویسه است.

جستجوی خارجی تهران→استانبول: `origin=IKA&originType=Airport&destination=IST&destinationType=City&isDomestic=false`، همراه با تاریخ و تعداد مسافران. مبدأ و مقصد بر اساس کاتالوگ اعتبارسنجی می‌شوند؛ مسیر دو شهر ایران در حالت خارجی رد می‌شود. در برگشت، کد و نوع هر دو طرف معکوس می‌شوند.

endpoint قبلی `/api/flights/airports` باقی است. برای URL قدیمی، نوع غایب `Airport` محسوب می‌شود و در نبود `isDomestic` مسیر از عضویت معتبر استنتاج می‌شود. فرودگاه منفرد عضو شهر چندفرودگاهی برای سازگاری URL قدیمی پذیرفته می‌شود؛ UI جدید فقط گزینه کل شهر را ارائه می‌کند. کد خارج از کاتالوگ با پیام فارسی و HTTP 400 رد می‌شود.

مسیرهای فرانت‌اند `/trip/flights` و `/trip/flights/search` هستند. نتایج SSR و فرم WASM با `prerender:false` است. حالت و نوع گزینه‌ها در query نتایج، فیلتر، صفحه‌بندی و لینک ادامه/ویرایش حفظ می‌شوند.

## بازتولید و آزمون

```powershell
python tools/generate-flight-search-catalog.py
dotnet ef migrations script 20260721052246_FlightAirportsReferenceData --idempotent --project src/Refahi.Modules.Flights.Infrastructure --startup-project src/Refahi.Modules.Flights.Infrastructure --context FlightsDbContext --output scripts/flights/search-catalog-20260906/01-schema.sql
dotnet test tests/Refahi.Modules.Flights.Tests/Refahi.Modules.Flights.Tests.csproj
```

برای آزمون PostgreSQL، متغیر `FLIGHT_CATALOG_TEST_CONNECTION` باید اتصال به سرور تست با مجوز ایجاد دیتابیس باشد. تست‌ها دیتابیس‌های تصادفی اختصاصی می‌سازند و پس از تست حذف می‌کنند؛ در نبود متغیر، تست‌های دیتابیس با علت مشخص skip می‌شوند.

برای smoke test واقعی `CITY`، متغیرهای `FLIGHT_SANDBOX_BASE_URL` و `FLIGHT_SANDBOX_API_KEY` را فقط برای محیط آزمایشی تأمین‌کننده تنظیم و تست `FlightCitySandboxTests` را اجرا کنید. این تست هیچ رزرو یا پرداختی انجام نمی‌دهد. تنظیمات production به‌صورت خودکار استفاده نمی‌شوند.

گزارش آزمون این تغییر در `validation.md` قرار دارد.
