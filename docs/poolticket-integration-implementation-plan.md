> Historical notes, superseded on 2026-10-13 by [current Pools architecture/API/runbook](pools-architecture-api-runbook.md). Routes, configuration and test counts below describe the retired implementation.

# پیاده‌سازی Pools — بکند و فرانتند

تاریخ: 2026-10-11

این سند طرح قبلی را با تصمیم‌های نهایی و پیاده‌سازی فعلی جایگزین می‌کند. مرجع API فایل `PoolTicket-Selected-API-Documentation.docx` است؛ دستورهای طرح قبلی دستور اجرایی مستقل نیستند. نام ماژول `Pools` است. کشف و محاسبه قیمت adapter واقعی دارند؛ رزرو، صدور، استعلام و لغو این فاز mock پایدار هستند. OAuth، coupon و انتقال آبسار در این فاز انجام نمی‌شوند.

## ساختار و مالکیت

بکند پنج پروژه `Refahi.Modules.Pools.{Domain,Application.Contracts,Application,Infrastructure,Api}` و schema اختصاصی `pools` دارد. پروژه‌ها، DI، endpoint mapping، migration اولیه و migration ثبت پایان لغو مالی در میزبان و solution ثبت شده‌اند. تنها وابستگی بین‌ماژولی تولیدی، قراردادهای Orders در Application است. هیچ reference یا فراخوانی مستقیم Wallet در Pools وجود ندارد.

فرانتند چهار پروژه `Refahi.Clients.Web.Modules.Pools.{Domain,Application,Infrastructure,UI}` دارد و در میزبان SSR، کلاینت WASM، DI و registry ثبت شده است. UI از RfPage، RfPageSection، RfhButton، RfhBottomSheet و LoginIsland موجود استفاده می‌کند. عنوان و فیلتر Pools و مسیر جزئیات سفارش در Profile ثبت شده‌اند.

PoolBooking مالک درخواست خرید، انتخاب بزرگسال/کودک، مبلغ IRR، snapshot، مرجع پرووایدر، مهلت، وضعیت‌ها، ارتباط Order و تلاش‌های عملیات است. Orders مالک Order/Payment و Wallets مالک برداشت و refund هستند. جدول‌ها:

- `pools.bookings`: درخواست، snapshot و ارتباط مالی؛ یکتایی کاربر/کلید درخواست، OrderId و provider/reference.
- `pools.provider_attempts`: intent و نتیجه Started/Completed/Failed/Ambiguous، پیش از تماس خارجی ذخیره می‌شوند.
- `pools.catalog_snapshots`: اطلاعات پایه و FetchedAt؛ کلید مرکب ProviderKey/ExternalId؛ مرجع ظرفیت یا قیمت جاری نیست.
- `pools.mock_reservations`: نتیجه پایدار lifecycle آزمایشی؛ با restart باقی می‌ماند.

شناسه خارجی رشته و شناسه داخلی خرید Guid است. `poolticket` تنها adapter ثبت‌شده است؛ `aabsar` برای فاز بعد محفوظ است. factory به پرووایدر دیگر fallback نمی‌کند. رابط‌های discovery/quote، reserve، confirm، status و cancel مستقل‌اند. قابلیت quote تا تأیید اثر calculate فعال نمی‌شود.

## قرارداد واقعی PoolTicket

Typed HttpClient عملیات زیر را زیر `api/v2/` اجرا می‌کند:

| عملیات | مسیر upstream | مسیر رفاهی زیر `/api/pools` |
|---|---|---|
| استان‌ها | POST states | GET /{provider}/states |
| شهرها | POST cities | GET /{provider}/cities |
| جست‌وجو | POST pools/{page}/{limit} | GET /{provider}/pools |
| سانس‌ها | POST saens/list | GET /{provider}/pools/{poolId}/sessions |
| قوانین | POST pool/terms-of-use/{poolId} | GET /{provider}/pools/{poolId}/terms |
| calculate | POST reservation/calculate | POST /{provider}/quotes |

`GET /providers` قابلیت‌ها را برمی‌گرداند. `GET /{provider}/pools/{poolId}` snapshot پایه، سانس و قوانین زنده را ترکیب می‌کند. upstream endpoint جزئیات پایه با شناسه ندارد؛ لینک مستقیم ناشناخته بدون نام/تصویر ساختگی سانس و قوانین را نمایش می‌دهد. پاسخ فهرست totalCount ندارد؛ UI تعداد کل یا تعداد صفحات نمی‌سازد.

نام‌های wire مانند saens، client_id و DTOهای snake_case در Infrastructure هستند. ClientId فقط سرور نگه داشته می‌شود. timeout پیش‌فرض ۳۰ ثانیه، circuit breaker پنج شکست/۳۰ ثانیه و هیچ retry خودکار POST وجود ندارد. status غیر از یک حتی با HTTP 200 خطاست. پاسخ ناقص، null، JSON خراب، timeout و خطاهای پرووایدر به خطای کنترل‌شده فارسی نگاشت می‌شوند؛ invalid_client خطای اتصال سرور است. HTML با whitelist همان کتابخانه سینما پاک‌سازی می‌شود؛ reference به سینما نداریم. log فقط operation، نتیجه، HTTP status، latency و trace است؛ payload و secret ثبت نمی‌شوند.

ورودی‌ها با FluentValidation کنترل می‌شوند: صفحه از یک، اندازه ۱۰/حداکثر۵۰، جنسیت صفر یا یک، مختصات جفت با محدوده معتبر و ناسازگار با شهر/منطقه، تعداد نامنفی و مجموع یک تا صد. پاسخ جنسیت دو حفظ می‌شود. فروش بزرگسال/کودک و تعلق ردیف به استخر دوباره بررسی می‌شود.

### تأییدهای لازم پیش از اتصال زنده

مستند واحد پول، timezone/قالب دقیق تاریخ، encoding بدنه و اثر calculate بر ظرفیت را قطعی نمی‌کند. هیچ تماس زنده‌ای برای تأیید این موارد اجرا نشده است.

- `PriceUnit` فقط پس از تأیید IRR یا TOMAN تنظیم شود؛ null هیچ مبلغ قابل پرداخت ایجاد نمی‌کند. TOMAN صریح در سرور ×۱۰ به long IRR تبدیل می‌شود؛ کسر ریال/overflow رد می‌شود.
- `CalculateReadOnlyConfirmed=true` فقط پس از تأیید بی‌اثر بودن calculate روی رزرو/ظرفیت تنظیم شود.
- `RequestEncoding=Form` انتخاب اولیه قابل تنظیم است، نه قرارداد تأییدشده؛ پس از تأیید، Form یا Json ثبت شود.
- تاریخ و ساعت نمایشی upstream دست‌نخورده می‌مانند. تبدیل زمان سانس به DateTimeOffset تا روشن شدن قرارداد انجام نمی‌شود.
- quote تضمین ظرفیت نیست. رزرو انتخاب و مبلغ را مجدداً بررسی می‌کند.
- خطای API واقعی به fixture یا قیمت ساختگی تبدیل نمی‌شود.

## چرخه خرید و کنترل مالی

1. انتخاب و quote؛ login از auth مرورگر موجود.
2. ذخیره booking/intent؛ lock کاربر+idempotency؛ همان کلید با انتخاب/مبلغ متفاوت conflict می‌دهد.
3. رزرو mock پایدار و دریافت reference/expiry؛ مهلت قابل پرداخت ۶۰ ثانیه پیش از expiry است.
4. `POST /bookings/{id}/checkout` با version و totalMinor؛ CreateOrderCommand از قرارداد Orders:
   - SourceModule=Pools، ReferenceType=PoolBooking، SourceReferenceId و SagaId برابر bookingId.
   - کلید `pools-order:{bookingId:N}` و PayableUntil رزرو.
   - یک آیتم مالی با quantity=1 و مبلغ نهایی؛ تفکیک تعدادها در snapshot.
   - CategoryCode=`entertainment.waterpark`.
5. هدایت به Checkout موجود `/checkout/orders/{orderId}?returnUrl=...`؛ تنها Orders از Wallet برداشت می‌کند.
6. پرداخت از Order دوباره بررسی می‌شود؛ سپس confirm mock و کد MOCK-TICKET صادر می‌شود. پرداخت و صدور دو وضعیت مجزا هستند.
7. لغو از Orders، بعد از تأیید لغو پرووایدر؛ refund دقیق به allocation اصلی Wallet.

ValidateOrderSourceNotification مالکیت، مرجع، مبلغ، دسته، مهلت و تغییرات مالی/ارسال غیرمجاز را رد می‌کند. IOrderPaymentParticipant زیر قفل مشترک PostgreSQL رزرو را دوباره استعلام می‌کند؛ IOrderCancellationParticipant پیش از refund لغو خارجی را آماده می‌کند. پرداخت و لغو رویدادهای موجود را مصرف می‌کنند. handler لغو، زیر قفل Orders تنها وضعیت commit‌شده را sync می‌کند و مجدداً فرمان لغو نمی‌فرستد.

قفل advisory بین میزبان‌ها، version concurrency و uniqueness دیتابیس از برداشت/رزرو/Order/صدور تکراری جلوگیری می‌کنند. Guidهای ساخته‌شده در دامنه در EF client-assigned هستند. ترتیب قفل financial سپس source رعایت می‌شود؛ Pools با قفل source فرمان لغو Orders نمی‌فرستد.

worker هر ۱۵ ثانیه، batch۵۰ با keyset pagination، intent رزرو نامشخص، Order بدون attachment، پرداخت بدون رویداد، صدور نامشخص، انقضا و لغو ناقص را بازیابی می‌کند. نتیجه نامشخص ابتدا status دارد و کورکورانه تکرار نمی‌شود؛ نبود پاسخ قطعی pending باقی می‌ماند. شکست قطعی صدور از CancelOrderCommand جبران می‌شود. refund پیش از تأیید لغو انجام نمی‌شود. پایان لغو مالی تنها پس از مشاهده Status=Cancelled از Orders ثبت می‌شود؛ درخواست بدون پرداخت هم پس از آن از صف worker خارج می‌شود.

APIهای خرید همگی UserOrAdmin و owner-only هستند و private/no-store دارند:

- POST /bookings
- GET /bookings/{id:guid}
- POST /bookings/{id:guid}/checkout
- POST /bookings/{id:guid}/cancel

endpoint پرداخت مستقیم، توکن پرووایدر و صدور اجباری وجود ندارد. همه پاسخ‌ها ApiResponseHelper و همه endpointها IEndpoint، WithName و WithTags دارند.

## محیط و راه‌اندازی

در appsettings پیش‌فرض تمام قابلیت‌های Pools خاموش‌اند. راه‌اندازی به‌صورت صریح انجام می‌شود؛ secret را در secret manager/environment سرور قرار دهید.

### Discovery واقعی

```json
{
  "Pools": {
    "CatalogEnabled": true,
    "PurchaseEnabled": false,
    "MockLifecycleEnabled": false,
    "FixtureCatalogEnabled": false,
    "TestFundsConfirmed": false,
    "PoolTicket": {
      "BaseUrl": "https://staging.poolticket.org",
      "TimeoutSeconds": 30,
      "RequestEncoding": "Form",
      "PriceUnit": null,
      "CalculateReadOnlyConfirmed": false
    }
  }
}
```

ClientId را از `Pools__PoolTicket__ClientId` یا secret store تامین کنید. BaseUrl بالا مقدار پیش‌فرض staging است؛ endpoint production باید توسط پرووایدر تأیید شود. تنظیم unit/encoding/calculate پس از تأیید به سند قرارداد افزوده شود.

migration schema pools توسط UsePoolsModule و DbTools موجود فقط هنگام CatalogEnabled اعمال می‌شود. در deploy از همان connection string نام‌گذاری‌شده پروژه استفاده می‌شود. پیش از فعال‌سازی migration را روی دیتابیس محیط مربوط اعمال کنید. مهاجرت این ماژول جدول‌های Commerce یا Orders را تغییر نمی‌دهد.

### خرید آزمایشی کامل و مستقل

فقط در Development، Test یا Staging و با دیتابیس/حساب‌ها/موجودی آزمایشی جدا:

```json
{
  "Pools": {
    "CatalogEnabled": true,
    "PurchaseEnabled": true,
    "MockLifecycleEnabled": true,
    "FixtureCatalogEnabled": true,
    "TestFundsConfirmed": true,
    "ReservationMinutes": 10,
    "SafetySeconds": 60,
    "WorkerSeconds": 15,
    "Mock": { "Scenario": "Success" }
  }
}
```

TestFundsConfirmed تأیید عملیاتی استفاده از داده و موجودی جداست؛ این flag به‌خودی‌خود موجودی واقعی را به موجودی آزمایشی تبدیل نمی‌کند. Wallet موجود نوع خاص «test balance» ندارد. اپراتور باید ابتدا جداسازی دیتابیس/حساب‌ها را برقرار کند؛ سپس این flag را فعال کند. بدون آن خرید mock حتی در محیط مجاز رد می‌شود.

FixtureCatalogEnabled=true fixture با مبلغ صریح IRR را انتخاب می‌کند. با false، discovery/calculate واقعی و lifecycle mock استفاده می‌شوند؛ اختلال اتصال واقعی هیچ fallback ندارد.

سناریوها: Success، Capacity، PriceChange، ReserveAmbiguous، ConfirmFailure، ConfirmAmbiguous، CancelAmbiguous. انقضا از مهلت ذخیره‌شده قابل آزمون است. سناریوهای Ambiguous ابتدا اثر بیرونی را ذخیره و سپس پاسخ را قطع می‌کنند؛ worker از status نتیجه را بازیابی می‌کند.

Production و هر محیط خارج سه نام مجاز با mock/fixture/purchase fail-fast می‌شود. endpoint رزرو، Checkout و مشارکت در پرداخت نیز policy را اجرا می‌کنند. خرید واقعی با configuration قابل فعال‌سازی نیست؛ adapter واقعی lifecycle و آزمون قرارداد باید ابتدا اضافه شوند.

## UI و رندر

| مسیر | رندر |
|---|---|
| /trip/pools | SSR؛ provider/state/city/search/gender/sort/page در URL |
| /trip/pools/{provider}/{poolId} | SSR برای اطلاعات، قوانین و سانس؛ island محدود انتخاب/quote، prerender=false |
| /trip/pools/checkout | InteractiveWebAssembly، prerender=false؛ sessionStorage، auth، بازبینی quote و رزرو |
| /trip/pools/bookings/{id:guid} | InteractiveWebAssembly، prerender=false؛ پرداخت مجدد، pending صدور، بلیت و لغو |
| /checkout/orders/{orderId:guid} | Checkout مشترک موجود |

تغییر provider فرم مستقل دارد و انتخاب مکان/فیلتر قبلی را پاک می‌کند. داده SSR serializable به island منتقل می‌شود و island در شروع داده را دوباره fetch نمی‌کند. draft فقط sessionStorage است؛ پس از رزرو پاک می‌شود و بکند مرجع قطعی است. draft خراب پاک و پیام فارسی نمایش داده می‌شود. پیش از POST رزرو، ReservationAttempted ذخیره می‌شود؛ پس از پاسخ نامشخص و refresh، کلید و مبلغ اولیه ثابت می‌مانند و فقط همان درخواست بازیابی می‌شود. رد قطعی ۴۰۹ پیش از ذخیره intent اجازه دریافت quote تازه و تأیید مبلغ جدید می‌دهد. خطای لغو نیز با GET تازه وضعیت را sync می‌کند تا بلیت در حال لغو نمایش داده نشود.

حالت‌های loading/empty/error، فروش بسته، مبلغ تغییرکرده، انقضا، پرداخت‌شده/صدور-pending و لغو-pending وجود دارند. عبارت «آزمایشی؛ فاقد اعتبار ورود» در خرید و بلیت mock نمایش داده می‌شود. polling وضعیت هر۱۰ثانیه تنها در تب قابل مشاهده و وضعیت pending است. CSS با توکن‌های تم در head میزبان بار می‌شود تا لینک مستقیم WASM هم style داشته باشد.

API network-only است. navigation معمولی network-first؛ مسیر خرید/بلیت Pools network-only و no-store است. service worker private/no-store را در page cache ذخیره نمی‌کند. inventory مخزن فرانتند و سند مرکزی به‌روز شده‌اند.

## فاز بعدی آبسار

adapter جدید با کلید aabsar و external IDs رشته‌ای به factory اضافه می‌شود. category entertainment.waterpark حفظ می‌شود. سفارش‌های تاریخی و باز Commerce مالکیت و مسیر قدیمی fulfillment/cancellation خود را نگه می‌دارند. شروع خرید جدید آبسار در Commerce فقط هنگام آماده‌شدن خرید Pools بسته می‌شود. هیچ reference به Infrastructure Commerce اضافه نمی‌شود.

## اعتبارسنجی و تحویل

تست‌های Pools در دو پروژه مستقل ثبت شده‌اند. تست PostgreSQL عمداً با EF InMemory جایگزین نمی‌شود؛ متغیر `REFAHI_POOLS_TEST_CONNECTION` باید دیتابیس جدا با نام شامل test را هدف بگیرد.

```powershell
# Backend، پس از تعیین connection محیط آزمایشی جدا
$env:REFAHI_POOLS_TEST_CONNECTION = '<isolated PostgreSQL connection; database name includes test>'
dotnet test tests/Refahi.Modules.Pools.Tests
dotnet test tests/Refahi.Modules.Orders.Tests
dotnet test tests/Refahi.Modules.Wallets.Tests
dotnet build Refahi.Backend.slnx

# Frontend؛ میزبان Development را با ApiBaseUrl=http://127.0.0.1:55732 اجرا کنید.
# Fixture server فقط هنگام اجرای تست مرورگر فعال است؛ Client APIها در مرورگر intercept می‌شوند.
$env:POOLS_UI_URL = 'http://127.0.0.1:55740'
dotnet test tests/Refahi.Clients.Web.Modules.Pools.Tests
dotnet build Refahi.Clients.Solution.sln
```

پوشش: DTOهای شش عملیات، واحد پول، payload نامعتبر، status=0، timeout و cancellation، پاک‌سازی HTML، policy Production/test-funds، idempotency، دستکاری Order، owner-only، قفل PostgreSQL، uniqueness، snapshot مرکب، keyset worker، پرداخت واقعی split allocation و محدودیت دسته، بازیابی با scope/service provider جدید، صدور نامشخص، شکست قطعی، انقضا، رقابت پرداخت/لغو و refund بدون تکرار.

تست Chromium: SSR بدون JavaScript، فیلتر URL/refresh/back، لینک جزئیات مستقیم، عدم fetch تکراری island، quote/رزرو/Checkout، draft خراب، auth مرورگر، بلیت mock، لغو، عرض موبایل۳۲۰/۳۷۵، screenshot RTL و PWA API/ticket cache.

تست‌های مرورگر از پاسخ‌های کنترل‌شده استفاده می‌کنند؛ تست مالی PostgreSQL خود Orders و Wallet را اجرا می‌کند. تست زنده PoolTicket و قرارداد خرید واقعی اجرا نشده‌اند. اعتبار mock جایگزین تأیید پرووایدر نیست.

### نتیجه اجرای 2026-10-11

- Pools بکند: ۳۲ تست موفق، بدون skip؛ PostgreSQL موقت جدا، migration واقعی، Kestrel HTTP API و Orders/Wallet واقعی با موجودی آزمایشی.
- Pools فرانتند: ۱۳ تست موفق، بدون skip؛ شامل شش تست Chromium، قطع پاسخ/تغییر قیمت/refresh و screenshot موبایل. API مرورگر/SSR fixture است.
- تست‌های متاثر موجود: Orders ۱۳، Wallets ۱۰، Commerce UI ۲۱ و Shell ۲۴ تست موفق.
- build هر دو solution موفق؛ فرانتند بدون warning/error؛ بکند دارای هشدارهای موجود خارج Pools.
- EF مدل و migration هماهنگ‌اند؛ git diff --check موفق.
- پروژه تست مستقل Checkout/Profile در مخزن یافت نشد؛ اتصال Checkout در تست مرورگر Pools و ثبت Profile/route provider با build بررسی شد.
- سرویس زنده PoolTicket، خرید واقعی پرووایدر، OAuth و coupon آزموده/فعال نشده‌اند؛ موارد آخر خارج دامنه این فازند.
