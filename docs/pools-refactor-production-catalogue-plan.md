# برنامهٔ ریفکتور Pools و آماده‌سازی Catalogue

تاریخ بررسی: 2026-10-13

وضعیت: برنامهٔ اجرایی بر اساس working tree فعلی؛ پیاده‌سازی شروع نشده است.

هدف: حذف Fake/Mock از مسیر اجرایی Pools، تکمیل ساختار CQRS در Application و آماده‌سازی Catalogue واقعی برای Production. آماده‌سازی Catalogue به معنی آماده بودن رزرو و صدور واقعی نیست.

## مبنای بررسی

- `AGENTS.md` مخزن بکند و فرانتند.
- پنج سند الزامی در `C:/Workspace/Refahi/docs`: overview، architecture، rendering governance، rendering inventory و migration report.
- `docs/poolticket-integration-implementation-plan.md` و متن قرارداد `docs/PoolTicket-Selected-API-Documentation.docx`.
- پنج پروژهٔ Pools، تنظیمات DI، provider/client واقعی و ساختگی، persistence، worker، endpointها و تست‌ها.
- الگوهای Handler مستقل در Hotels و Catalogue در Commerce، قراردادهای Orders و integrationهای فعلی Pools.
- سرویس API، مدل‌ها، صفحات Catalogue/Details، island خرید و route helperهای Pools در فرانتند.

اسناد عمومی در `docs` بکند یافت نشدند؛ نسخه‌های موجود در مسیر مستندات مرکزی خوانده شدند. گزارش migration مربوط به 2026-06-01 است و برخی پیشنهادهای قدیمی آن با قانون فعلی SSR/WASM ناسازگارند؛ قانون فعلی و AGENTS مبنای تصمیم هستند.

working tree بکند تغییرات ثبت‌نشده در Contracts، Validators، Domain، providerها و appsettings دارد. این برنامه بر مبنای همان وضعیت است و جایگزینی آن تغییرات را تجویز نمی‌کند. پیش از اجرای برنامه باید baseline جاری تثبیت و تغییرات مرتبط در همان مسیر ادامه داده شوند.

## یافته‌های فعلی

| موضوع | وضعیت مشاهده‌شده | اثر بر برنامه |
|---|---|---|
| معماری | پنج پروژه و schema اختصاصی `pools` موجود است؛ ارتباط مالی از قراردادهای Orders انجام می‌شود | ساختار حفظ شود؛ ماژول Catalogue عمومی یا مسیر پرداخت تازه ساخته نشود |
| CQRS | Requestها، MediatR و FluentValidation موجودند؛ هشت Query در `PoolCatalogHandlers` و عملیات Booking در Handler تجمیعی و `PoolBookingService` هستند | CQRS از صفر ساخته نمی‌شود؛ use caseها و مسئولیت‌ها تفکیک می‌شوند |
| Query رزرو | `GetPoolBookingQuery` از `GetAsync` استفاده می‌کند که وضعیت مالی را در دیتابیس تغییر می‌دهد | همگام‌سازی به Command/reconciliation منتقل شود؛ Query صرفاً وضعیت را بخواند |
| Mock اجرایی | `FixturePoolProvider`، `MockPoolLifecycle`، `PoolProviderWithMock`، `MockPoolOptions` و `MockPoolReservation` در Infrastructure هستند | حذف کلاس‌ها، registrationها، تنظیمات و storage ساختگی در دامنهٔ اجرا |
| هویت داده | Fixture و provider واقعی هر دو کلید `poolticket` دارند؛ Fixture از شناسهٔ `1` استفاده می‌کند | پاک‌سازی snapshot با شرط provider یا ID به‌تنهایی قابل اعتماد نیست |
| Catalogue واقعی | استان، شهر، فهرست، سانس، قوانین و calculate adapter دارند | adapter فعلی اصلاح و آزموده شود؛ بازنویسی کامل لازم نیست |
| جزئیات | قرارداد منتخب endpoint اطلاعات پایه با شناسه ندارد؛ Summary از snapshot جست‌وجو تأمین می‌شود | لینک مستقیم بدون snapshot و عمر اطلاعات پایه باید قرارداد روشن داشته باشند |
| snapshot | `FetchedAt` ذخیره می‌شود، ولی store فقط Summary برمی‌گرداند؛ expiry اعمال نمی‌شود | تازگی و محدودیت استفاده از snapshot باید صریح شوند |
| هزینهٔ جست‌وجو | پاسخ upstream پیش از برگشت، در دیتابیس upsert می‌شود؛ هر آیتم lookup جدا دارد و refreshها قفل per-provider دارند | وابستگی latency فهرست به persistence و تعداد queryها کاهش یابد |
| paging | upstream `totalCount` ندارد؛ adapter فعلی page/limit درخواست را برمی‌گرداند | metadata واقعی upstream اعتبارسنجی شود؛ تعداد کل ساخته نشود |
| فیلتر استان | `StateId` به فهرست upstream فرستاده نمی‌شود و فقط برای city picker است | انتخاب استان نباید به‌اشتباه فیلتر واقعی نتایج تلقی شود |
| پول و زمان | واحد قیمت قابل تنظیم و null است؛ تاریخ/ساعت رشتهٔ upstream باقی می‌ماند | نمایش قیمت و تبدیل زمان بدون قرارداد تأییدشده قطعی نشود |
| resilience | timeout، circuit breaker و log latency وجود دارند؛ retry خودکار وجود ندارد | رفتار HTTP و payload ناقص کامل‌تر شود؛ POSTها بدون تأیید تکرار نشوند |
| خرید واقعی | provider واقعی قابلیت reserve/confirm/status/cancel ندارد؛ policy خرید غیرmock را رد می‌کند | پایان این پروژه Catalogue واقعی و خرید غیرفعال است |
| فرانتند | routeهای صفحات `/pools` هستند، ولی helperهای Details/Booking و inventoryهای مرکزی و محلی `/trip/pools` دارند | لینک‌های فهرست و بازگشت پرداخت باید با route واقعی هماهنگ شوند |

اعتبارسنجی baseline: اجرای `dotnet build tests/Refahi.Modules.Pools.Tests/Refahi.Modules.Pools.Tests.csproj --no-restore --verbosity quiet` ناموفق بود. دو خطای CS0246 در `PoolContractTests.cs` برای `PoolSearchValidator` و `PoolSelectionValidator` رخ دادند؛ کلاس‌ها اکنون در namespace `Application.Validators` هستند، اما تست فایل namespace قبلی را import می‌کند. این خطا قبل از ریفکتور مشاهده شد. سه هشدار NU1510 در Wallets نیز وجود داشت. تست‌ها اجرا نشدند.

## مرز تحویل و تصمیم‌های پیشنهادی

1. هیچ provider، قیمت، سانس، بلیت یا fallback ساختگی در برنامهٔ deployشده باقی نماند؛ خطای upstream به empty موفق یا دادهٔ نمونه تبدیل نشود.
2. پاسخ‌های کنترل‌شده و test doubleهای مستقل فقط در پروژه‌های tests بمانند. این فرض منتظر پاسخ کاربر است؛ حذف Mock اجرایی نباید به حذف پوشش خطا، امنیت و بازیابی منجر شود.
3. Catalogue شامل providers، states، cities، search، details، sessions و terms است. Quote قابلیت جداگانه و مشروط به تأیید calculate دارد.
4. خرید واقعی، OAuth مستقل، coupon، provider آبسار، redesign UI و تغییر معماری auth در این تحویل نیستند.
5. ساختار پنج پروژه، provider abstraction، شناسه‌های خارجی و مسیر `Pools → Orders → Wallet` حفظ شوند.
6. مسیر canonical پیشنهادی بر اساس route فعلی `/pools` است. helperها، لینک‌های بازگشت، registry و مستندات هماهنگ شوند؛ اگر لینک‌های `/trip/pools` قبلاً منتشر شده‌اند، redirect سازگار در SSR حفظ و در inventory ثبت شود.
7. تغییر نسخهٔ MediatR بخشی از این کار نیست. پروژه‌های Pools در عمل 11.1.0 دارند، اگرچه سند عمومی عدد دیگری ذکر می‌کند؛ از API و نسخهٔ موجود مخزن استفاده شود.

## ترتیب اجرای پیشنهادی

### گام صفر — تثبیت baseline و قرارداد اتصال

کارها:

- اصلاح import تست‌ها در دامنهٔ تغییرات فعلی و گرفتن build و نتیجهٔ تست قابل اتکا؛ تفکیک خطاهای قبلی از ریفکتور.
- ثبت نسخهٔ قرارداد PoolTicket و دریافت تأیید production BaseUrl، ClientId/دسترسی، encoding، واحد پول و محدودیت درخواست.
- تأیید قالب تاریخ، تقویم و timezone پیش از هر تبدیل زمان؛ تا آن زمان نمایش رشتهٔ تأمین‌کننده بدون تفسیر جدید.
- گرفتن نمونهٔ پاسخ واقعیِ بدون secret برای فهرست، صفحهٔ خالی، سانس، قوانین و خطاها؛ استفاده برای contract test.
- مشخص کردن اینکه دیتابیس هر محیط دادهٔ Fixture یا Booking آزمایشی مرتبط با Order دارد یا خیر.

شرط خروج: baseline و فهرست تأییدهای provider روشن باشند. نبود تأیید provider مانع کار ساختاری نیست، اما مانع ادعای آماده بودن اتصال Production است.

### گام یک — حذف Mock اجرایی و مدیریت دادهٔ موجود

فایل‌ها و نقاط اثر:

- حذف `Providers/FixturePoolProvider.cs` و `Providers/MockPoolLifecycle.cs` شامل decorator و options آنها.
- حذف registration و selection ساختگی در `Infrastructure/DI.cs`؛ ثبت مستقیم provider واقعی از طریق factory موجود.
- حذف `FixtureCatalogEnabled`، `MockLifecycleEnabled`، `TestFundsConfirmed` و بخش `Pools:Mock` از options و فایل‌های تنظیمات، مثال‌های deploy و مستندات جاری.
- بازنویسی validation تنظیمات و purchase policy: Catalogue مستقل از Mock؛ خرید تا پیاده‌سازی provider واقعی همیشه بسته؛ flag قدیمی یا ناسازگار در پیکربندی باید با خطای روشن شناسایی شود.
- حذف entity، DbSet و mapping `MockPoolReservation` از مدل جاری؛ migration جدید برای جمع‌آوری storage آن، پس از بررسی داده. migrationهای اعمال‌شدهٔ قبلی بازنویسی نشوند.
- حذف وابستگی منطق جاری به IsMock و پیام‌های ساختگی در UI و capabilities. فیلدهای تاریخی Booking/Order فقط در صورت اثبات عدم وجود رکورد یا پس از طرح سازگار حذف شوند؛ حفظ نشانهٔ منشأ تاریخی مجوز اجرای mock نیست.
- انتقال شبیه‌سازی مورد نیاز تست‌های مالی به test double داخل پروژهٔ tests؛ هیچ reference از src به آن نماند.
- بازنویسی تست‌های policy، DI، مالی و مرورگر وابسته به کلاس‌ها و flags حذف‌شده؛ تست‌های مفید حذف نشوند.

پاک‌سازی داده:

- snapshotهای ساختگی و واقعی می‌توانند provider/ID یکسان داشته باشند؛ حذف شرطی بر مبنای `poolticket`/`1` ممنوع است مگر منشأ داده ثابت شود.
- برای محیطی که واقعاً Fixture داشته و منشأ snapshot معلوم نیست، بازسازی کل projection متعلق به Pools از دادهٔ واقعی یک گزینه است؛ تأثیر آن بر Summary لینک مستقیم باید در rollout ثبت شود. این کار روی Booking snapshot یا سند مالی انجام نشود.
- داده‌های test در دیتابیس‌های کاملاً جدا پس از inventory قابل جمع‌آوری‌اند. در محیط مشترک، Booking مرتبط با Order ابتدا از نظر پرداخت/لغو بررسی شود؛ حذف مستقیم Order/Wallet ledger ممنوع است.
- پیش از حذف lifecycle، رزروهای mock باز و سفارش‌های مرتبط تعیین تکلیف شوند؛ حذف کلاس‌ها نباید recovery/refund رکوردهای موجود را بی‌صدا قطع کند.

شرط خروج: سرویس deployشده هیچ مسیر ساختگی ندارد؛ خطای provider همان خطای کنترل‌شده است؛ خرید جدید غیرفعال و سوابق مالی قابل پیگیری‌اند.

### گام دو — تفکیک CQRS بر اساس use case

ساختار Application:

```text
Features/
  Catalog/
    GetProviders/
    GetStates/
    GetCities/
    SearchPools/
    GetPoolDetails/
    GetPoolSessions/
    GetPoolTerms/
    GetPoolQuote/
  Bookings/
    ReservePoolBooking/
    CheckoutPoolBooking/
    GetPoolBooking/
    CancelPoolBooking/
    ReconcilePoolBooking/
```

- برای هر Request، Handler مستقل و Validator مرتبط در پوشهٔ action قرار گیرد. قراردادهای عمومی فعلی در Application.Contracts باقی بمانند؛ تغییر نام/شکل عمومی فقط برای نیاز واقعی.
- `PoolCatalogHandlers` و `PoolBookingHandlers` تجمیعی حذف شوند؛ صرفاً اضافه کردن wrapperهای تازه دور همان سرویس بزرگ پایان ریفکتور نیست.
- مسئولیت‌های reserve، checkout و cancel از `PoolBookingService` به use caseهای مشخص منتقل شوند؛ رفتار مشترک مالکیت، snapshot mapping و lifecycle فقط در صورت مصرف مشترک نگه داشته شود.
- state transition و invariantها در Domain، IO و provider mapping در Infrastructure و orchestration در Application بمانند.
- `GetPoolBookingQuery` خواندنی شود؛ sync وضعیت Orders، finalize و reconciliation در Commandها/رویدادهای مستقل اجرا شوند. نحوهٔ تازگی وضعیت نمایش باید آزموده شود.
- Queryهای Catalogue وضعیت Booking/Order را تغییر ندهند. نگه‌داری projection حاصل از provider با منطق مستقل refresh انجام شود؛ اگر Search همچنان refresh projection را trigger می‌کند، این اثر در قرارداد و تست مشخص باشد. کش و projection با وضعیت کسب‌وکار اشتباه گرفته نشوند.
- event handlerهای OrderPaid/OrderCancelled و payment/cancellation participantها مسئولیت مشخص و مستقل داشته باشند؛ ترتیب قفل مالی سپس source، owner check و idempotency حفظ شوند.
- `IPoolBookingRepository` در Domain و قراردادهای provider در Application.Contracts حفظ شوند. abstraction اضافه، DbContext دوم یا CQRS framework جدید لازم نیست.
- DI و pipeline اعتبارسنجی با IRequestHandlerهای جدید بررسی شوند؛ هر Request فقط یک Handler داشته باشد و CancellationToken تا IO منتقل شود.
- در Api، Catalogue endpointها در فایل‌های action یا گروه Catalogue از Booking جدا شوند؛ مسیرها، نام‌ها و قالب ApiResponse حفظ و statusهای واقعی مستند شوند.

شرط خروج: هر use case مسیر خوانا و مستقل دارد؛ Query رزرو write نمی‌کند؛ رفتار مالی و قرارداد عمومی ناخواسته تغییر نکرده است.

### گام سه — تکمیل Catalogue برای بار و خطای واقعی

**قرارداد و validation**

- DTOهای wire از DTO عمومی جدا بمانند؛ دادهٔ خام provider به مصرف‌کننده ارسال نشود.
- فیلدهای ضروری wire به شکلی deserialize شوند که missing با مقدار صفر/false معتبر اشتباه نشود؛ به‌خصوص مبلغ سانس، flags فروش، page/limit و شناسه‌ها.
- null، دادهٔ ناقص، شناسهٔ تکراری، قیمت منفی، مختصات نامعتبر و HTML/URL ناامن پوشش داده شوند؛ فیلدهای واقعاً nullable بدون دادهٔ جایگزین ساختگی حفظ شوند.
- جنسیت پاسخ 0/1/2 با جنسیت فیلتر 0/1 اشتباه نشود؛ sort از قرارداد provider اعتبارسنجی شود.
- StateId تنها برای picker است؛ جست‌وجوی استان به‌تنهایی نباید ادعا شود. ناسازگاری استان/شهر کنترل و رفتار انتخاب استان بدون شهر در UI روشن شود.
- status فروش کل استخر و بلیت از سایر محصولات مثل course/pack تفکیک شود؛ در صورت تأیید provider، `just_active_sell_ticket` و `status_sell_ticket` برای دامنهٔ بلیت استفاده شوند.
- page/limit برگشتی upstream validate شود. totalCount/totalPages ساخته نشوند؛ صفحهٔ کامل صرفاً احتمال وجود صفحهٔ بعد است و صفحهٔ بعد خالی یک حالت معتبر است.

**جزئیات و freshness**

- Summary فقط اطلاعات مشاهده‌شده از provider باشد؛ عمر snapshot از FetchedAt خوانده و در قرارداد داخلی/عمومی متناسب با نیاز UI ثبت شود.
- نبود Summary به‌تنهایی اثبات عدم وجود استخر نیست. اگر sessions/terms برای ID معتبر موفق باشند، نمایش بدون نام/تصویر ساختگی و با حالت روشن ادامه یابد؛ invalid_pool واقعی 404 باشد.
- در این تحویل تضمین جزئیات پایهٔ کامل برای لینک مستقیم داده نشود؛ تضمین آن نیازمند endpoint رسمی یا روش sync مورد تأیید provider است.
- سیاست stale و شکست upstream صریح باشد؛ دادهٔ منقضیِ سانس یا قیمت به‌عنوان وضعیت جاری فروخته نشود. snapshot نمایش هیچ‌گاه مجوز ظرفیت/پرداخت نیست.

**کارایی و کش**

- از `ICacheService` مشترک استفاده شود؛ سرویس cache موازی یا Dapper بدون شاهد بار لازم نیست.
- TTL اولیهٔ پیشنهادی و قابل بازبینی با provider: استان/شهر ۶ ساعت، فهرست ۶۰ ثانیه، قوانین ۱۵ دقیقه؛ سانس در انتشار نخست live و quote بدون cache. این اعداد تصمیم اولیه‌اند، نه SLA تأمین‌کننده.
- cache key شامل provider، نسخه/محیط منبع و تمام فیلترهای مؤثر، page و limit باشد؛ دادهٔ staging/Fixture وارد cache Production نشود.
- تنها نتیجهٔ موفق و validateشده cache شود؛ empty معتبر با اختلال provider یکسان نشود. شکست cache موجب تولید پاسخ ساختگی یا شکست غیرضروری Catalogue نشود.
- snapshot upsert از lookup per-item به دریافت batch و update/insert کنترل‌شده تبدیل شود؛ امنیت uniqueness و concurrency حفظ شود.
- سیاست شکست ذخیرهٔ projection مشخص شود: اگر دادهٔ provider معتبر است، اختلال ذخیره باید log/metric قابل اقدام داشته باشد؛ در صورت انتخاب پاسخ موفقِ بدون ذخیره، محدودیت Summary بعدی به‌صورت روشن آزموده شود.
- تعداد تماس‌های صفحهٔ SSR و زمان endpoints اندازه‌گیری شود؛ همزمان‌سازی calls مستقل فقط پس از تفکیک نیاز و رعایت محدودیت upstream.

**HTTP، امنیت و مشاهده‌پذیری**

- اعتبارسنجی startup مشروط به CatalogEnabled: endpoint Production تأییدشده، ClientId غیرخالی، encoding معتبر، timeout و price unit قابل دفاع. secrets فقط در secret store/environment.
- غیرJSON، body خالی، content type نامعتبر، HTTP 429/5xx و خطای status=0 به پاسخ فارسی مناسب map شوند؛ خطای upstream کاربر را logout نکند.
- timeout و cancellation کاربر جدا بمانند؛ body size و خواندن پاسخ محدود شوند تا payload بزرگ منابع را مصرف نکند.
- circuit breaker موجود حفظ شود؛ retry فقط برای عملیات POST خواندنی که تکرارشان توسط provider تأیید شده، با تعداد محدود و رعایت Retry-After. calculate و lifecycle خودکار تکرار نشوند.
- نرخ و concurrency تماس‌های عمومی با الگوی موجود میزبان محدود شود؛ سقف‌ها با quota تأمین‌کننده و آزمون بار تعیین شوند.
- log ساختاریافتهٔ operation/provider/http status/outcome/latency/trace حفظ و cache hit/miss و شکست projection قابل مشاهده شوند؛ secret و payload حساس ثبت نشود.
- check پیکربندی و سنجش اتصال provider با health/readiness موجود میزبان ادغام شود؛ probe پرتکرار نباید quota را مصرف کند. خرابی provider فرایند میزبان را به restart loop نکشاند.

شرط خروج: فهرست و سانس واقعی، دادهٔ ناقص و قطع اتصال را کنترل می‌کنند؛ هزینهٔ calls و freshness مشخص‌اند؛ اتصال Production در محیط واقعی تأیید شده است.

### گام چهار — سازگاری مصرف‌کنندگان و انتشار

- همگام‌سازی مدل‌های فرانتند، API service، route helperها، Profile links، registry و returnUrlها با قرارداد و route نهایی؛ جست‌وجوی تمام callers قبل از حذف فیلد عمومی.
- Catalogue/Details همچنان SSR و URL-driven باشند؛ جزیرهٔ quote فقط در صورت فعال بودن قابلیت لازم render شود و دادهٔ آغازین را دوباره fetch نکند.
- UI بدون خرید، دکمهٔ رزرو/Checkout فعال نداشته باشد؛ stateهای empty، خطا، فروش غیرفعال، نبود Summary و واحد نامعلوم واضح باشند.
- routeهای transactional موجود بدون جعل بلیت یا حذف دسترسی به سوابق تاریخی مدیریت شوند؛ WASM با `prerender:false` حفظ شود.
- inventory محلی فرانتند و مرکزی برای routeهای واقعی و حذف توضیحات mock همگام شوند؛ route جدید احتمالی redirect نیز ثبت شود.
- سند فعلی integration به‌روز شود؛ سناریوی خرید mock از دستورهای فعال deploy حذف و در صورت نیاز صرفاً تاریخچه معرفی شود.
- migration روی PostgreSQL آزمایشی، smoke test در staging واقعی، سپس فعال‌سازی فقط Catalogue با خرید بسته انجام شود.
- برای rollback، بازگشت به مسیر خرید mock مجاز نباشد؛ خاموش کردن Catalogue و rollback تغییر برنامه با حفظ اسناد مالی مسیر امن اولیه است. migration حذف داده فقط پس از backup/بررسی و با راه بازگردانی روشن اجرا شود.

## معیار پذیرش و آزمون

| بخش | شاهد لازم برای پایان کار |
|---|---|
| حذف ساختگی | جست‌وجوی src/config نشان دهد هیچ registration/provider/lifecycle ساختگی اجرایی نیست؛ deployment قدیمی نتواند آن را فعال کند |
| CQRS | هر Request Handler مستقل؛ validation از MediatR؛ Query رزرو بدون SaveChanges؛ read model refresh مشخص |
| قرارداد provider | نمونهٔ واقعی validateشده؛ missing/null/duplicate/negative/HTML/HTTP errors/cancellation پوشش داده شوند |
| Catalogue | states/cities/search/details/sessions/terms موفق؛ فیلتر، sort و صفحهٔ خالی صحیح؛ total ساختگی وجود ندارد |
| persistence | migration واقعی PostgreSQL؛ snapshot مرکب؛ تازگی و rebuild projection؛ عدم حذف تاریخچهٔ مالی |
| کارایی | شمار calls و queryهای DB ثبت؛ cache keys و TTL صحیح؛ آزمون بار با هدف توافق‌شده و رعایت quota |
| امنیت | secret در پاسخ/log/browser نیست؛ sanitized HTML؛ مالکیت Booking و authهای موجود محفوظ |
| خرید | capability خرید false و تلاش direct API نیز مسدود؛ هیچ Order یا debit جدید ناشی از discovery/quote ایجاد نشود |
| رگرسیون مالی | تست idempotency، participantها، lock ordering و refund سوابق تحت تأثیر در PostgreSQL جدا اجرا شود |
| فرانتند | لینک کارت به route واقعی؛ SSR بدون JS، refresh/back فیلترها، نبود fetch تکراری، RTL/mobile و حالت خطا |
| PWA | API network-only؛ navigation طبق governance؛ بعد از تغییر WASM hard refresh و نسخهٔ worker بررسی شود |
| انتشار | build بکند/فرانتند و تست‌های مرتبط پاس؛ staging واقعی و تأیید تنظیمات Production ثبت؛ docs/inventory هماهنگ |

دستورهای آزمون بر اساس فایل‌های موجود:

```powershell
dotnet test tests/Refahi.Modules.Pools.Tests/Refahi.Modules.Pools.Tests.csproj
dotnet test tests/Refahi.Modules.Orders.Tests/Refahi.Modules.Orders.Tests.csproj
dotnet test tests/Refahi.Modules.Wallets.Tests/Refahi.Modules.Wallets.Tests.csproj
dotnet build Refahi.Backend.slnx
# در مخزن refahiplus-webapp
dotnet test tests/Refahi.Clients.Web.Modules.Pools.Tests/Refahi.Clients.Web.Modules.Pools.Tests.csproj
dotnet build Refahi.Clients.Solution.sln
```

تست PostgreSQL Pools نیازمند `REFAHI_POOLS_TEST_CONNECTION` به دیتابیس مستقل با نام شامل test است؛ از اتصال اجرایی برای تست یا پاک‌سازی استفاده نشود.

## تقسیم پیشنهادی تغییرات قابل بازبینی

1. baseline و حذف مسیر Mock از src/config، policy و تست‌های جایگزین، همراه طرح سازگار دادهٔ موجود.
2. تفکیک use caseهای CQRS و انتقال write از Query رزرو، با رگرسیون مالی.
3. hardening Catalogue: wire validation، snapshot/freshness، cache، HTTP و observability.
4. هماهنگی consumers/routeها/docs/inventory و شواهد staging/Production؛ migration پاک‌سازی storage پس از تعیین تکلیف داده‌ها.

هر تغییر باید مستقل قابل بررسی باشد؛ حذف Mock به‌تنهایی برچسب Production Ready نمی‌گیرد. شروع پیاده‌سازی خرید واقعی پروژهٔ جدا با قرارداد reserve/confirm/status/cancel و verification مسیر Orders/Wallet است.

## تصمیم‌های باز

- تأیید دامنهٔ حذف Mock: مسیر اجرایی با حفظ ابزار تست، یا حذف شبیه‌سازی از تست‌ها نیز؟ فرض پیشنهادی گزینهٔ اول است.
- مشخصات Production و قرارداد واحد پول/encoding/date/timezone/quota هنوز از کاربر دریافت نشده‌اند؛ در اسناد موجود قطعی نیستند.
- وجود رزرو/Order آزمایشی باز و منشأ snapshotها نیازمند inventory محیط است؛ در این بررسی دیتابیس خوانده نشده است.
- تضمین Summary کامل لینک مستقیم خارج از دادهٔ مشاهده‌شده، نیازمند قابلیت رسمی upstream است.
- هدف latency و حجم ترافیک باید پیش از آزمون بار توافق شوند؛ در این بررسی SLA عددی ساخته نشده است.

محدودیت بررسی: تنها build پروژهٔ تست Pools برای baseline اجرا شد و شکست خورد. تست زندهٔ provider، تست PostgreSQL، آزمون مرورگر و بار اجرا نشدند. هیچ کد اجرایی، دادهٔ دیتابیس یا تنظیمات موجود تغییر نکرده است.
