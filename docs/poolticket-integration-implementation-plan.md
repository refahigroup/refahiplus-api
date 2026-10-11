# طرح پیاده‌سازی PoolTicket در Refahi

## هدف و دامنه

این سند، قرارداد اجرایی اتصال Refahi به PoolTicket و کار لازم برای Backend و WebApp را مشخص می‌کند. مرجع رفتار provider، فایل `PoolTicket-Selected-API-Documentation.docx` نسخه 2.0.0 است؛ collection دستی Bruno در پوشه `PoolTicket Bruno Collection` برای اجرای دستی همین قرارداد ساخته شده است.

ماژول مقصد در Backend، scaffold پنج‌لایهٔ موجود `Refahi.Modules.Pool.*` است. این پوشه‌ها در مخزن فعلی فقط `bin/obj` دارند و سورس، پروژه‌های `.csproj` و endpoint ندارند؛ در `Program.cs` و `Refahi.Api.csproj` نیز ثبت نشده‌اند. در WebApp هیچ ماژول Pool پیدا نشد. پیاده‌سازی باید در ساختار همین دو مخزن انجام شود:

- Backend: `C:\Workspace\repo\refahiplus-api`
- WebApp: `C:\Workspace\repo\refahiplus-webapp`

این نسخه فقط **کشف استخر، نمایش سانس‌ها/قوانین و محاسبهٔ قیمت** را تعریف می‌کند. سند PoolTicket هیچ endpoint برای ایجاد رزرو نهایی، نگه‌داشتن ظرفیت، تأیید/صدور بلیت، استعلام وضعیت رزرو، لغو یا refund ارائه نمی‌کند. بنابراین دکمهٔ خرید، تبدیل به Order و پرداخت Wallet تا دریافت قرارداد مستند و قابل‌آزمون آن عملیات نباید فعال شوند. پاسخ calculate رسید یا تضمین رزرو نیست.

## یافته‌های قرارداد PoolTicket

Base URL محیط آزمایشی: `https://staging.poolticket.org`، مسیرها زیر `/api/v2` هستند. PoolTicket در همهٔ عملیات `client_id` می‌خواهد؛ مقدار client token باید فقط از تنظیمات محرمانهٔ Backend خوانده و در همین فیلد ارسال شود. مقدار توکن داده‌شده در گفتگو صرفاً تستی است و نباید در source code، لاگ، فرانت، appsettings committed یا فایل تنظیمات production قرار گیرد.

| عملیات provider | درخواست | بدنه/پارامترهای مستندشده | دادهٔ مهم پاسخ |
|---|---|---|---|
| استان‌ها | `POST /states` | `client_id`, اختیاری `just_active_states` | `states[]: state_id, name` |
| شهرها | `POST /cities` | `client_id`, اختیاری `state_id`, `just_active_cities` | `cities[]: state_id, state_name, city_id, city_name`, `latest_all_cities_update` |
| جست‌وجوی استخر | `POST /pools/{page}/{limit}` | `client_id`; فیلترهای اختیاری pool/location/gender/sales/type/sort | `pools[]` شامل شناسه، نام، مختصات، وضعیت فروش، موقعیت، تصویر، تخفیف، امتیاز و حداقل قیمت؛ `sort_list`, `sorted_by`, `map_settings` |
| سانس‌های استخر | `POST /saens/list` | `client_id`, `pool_id`, اختیاری `access_token` | `saens[]` و `rows[]`؛ شناسه ردیف، قیمت بزرگسال/کودک، وضعیت فروش، تاریخ/روز/زمان، توضیح و قوانین |
| شرایط استخر | `POST /pool/terms-of-use/{poolId}` | `client_id`, `pool_id` | `terms_of_use.content` |
| محاسبه قیمت | `POST /reservation/calculate` | `client_id`, `saens_row_id`, `count_adult`, `count_child`; اختیاری `coupon`, `access_token` | `cart`: قیمت بازار/فروش، coupon، اعتبار تخفیف، نوع تخفیف، تعداد و تاریخ/زمان سانس |
| دریافت توکن | `POST` یا `GET /token` | `grant_type=password`, `username`, `password`, `client_id`, `scope=front|operator`; اختیاری device/metadata | access/refresh token و شناسه/اطلاعات user یا operator |
| اعتبارسنجی/تازه‌سازی توکن | `POST` یا `GET /token` | `grant_type=check_token`, `client_id`, `scope`, اختیاری access/refresh/device | اطلاعات user یا operator یا خطای token |

در جست‌وجوی استخر، یکی از دو گروه مکانی مجاز است: `city_id`/`area_id` یا `lat`/`lng`؛ ارسال همزمان هر دو گروه خطای مستندشده دارد. `gender`: صفر مردانه، یک زنانه، دو همه؛ `sort_by` نمونه‌ها: `favorite`, `visit_count`, `comment`, `discount`, `name`. وضعیت‌های فروش و دیگر فیلترها اختیاری‌اند.

مقادیر و قراردادهایی که سند روشن نکرده است: الگوریتم encode گذرواژهٔ scope `front`، قالب دقیق تاریخ‌ها، رفتار اثرگذار/بدون‌اثر calculate بر ظرفیت، نوع محتوای بدنه برای GET، حدود pagination و قرارداد کامل خطاها. نمونهٔ cities سند نیز trailing comma دارد و JSON معتبر نیست. Adapter باید JSON واقعی را بر اساس DTOهای typed deserialize کند، نه اینکه نمونه‌های ناقص سند را کپی کند.

خطاهای شناخته‌شده شامل `invalid_client`, `invalid_state`, `invalid_pool`, `invalid_saens_row`, `pool_capacity_overflow`, `not_available_ticket_serial`, `coupon_error`, `mobile_not_approved`, `invalid_metadata`, `invalid_scope`, `unsupported_grant_type`, `invalid_refresh_token`, `invalid_access_token` و `expired_access_token` هستند. متن خام provider نباید به کاربر نهایی نمایش داده شود.

## معماری Backend

پروژه‌های پنج‌گانهٔ backend را با نام‌های زیر بسازید و طبق الگوی ماژول‌های موجود در `Refahi.Backend.slnx`، `Refahi.Api.csproj`, `Program.cs` و فایل‌های DI ثبت کنید:

- `Refahi.Modules.Pool.Domain`
- `Refahi.Modules.Pool.Application.Contracts`
- `Refahi.Modules.Pool.Application`
- `Refahi.Modules.Pool.Infrastructure`
- `Refahi.Modules.Pool.Api`

ساختار پیشنهادی:

- `Domain`: در فاز discovery هیچ Aggregate رزرو نسازید؛ داده‌های provider، بلیت یا pool را مالک Refahi فرض نکنید.
- `Application.Contracts`: Query/DTOهای داخلی پایدار و بدون نوع‌های HTTP/JSON مخصوص PoolTicket.
- `Application`: Queryها، handlerها و FluentValidation برای فیلتر شهر/مختصات، pagination، pool و محاسبه.
- `Infrastructure`: `PoolTicketOptions`, typed `HttpClient`, DTOهای wire خصوصی، mapper و adapter `IPoolTicketClient`/`PoolTicketClient`.
- `Api`: endpointهای Minimal API با `IEndpoint`, `ApiResponseHelper`, `WithName`, `WithTags`, cancellation token و پیام خطای فارسی.

برای نسخهٔ اول نیازی به کپی کردن کاتالوگ استخرها در دیتابیس نیست. اگر implementation زیرساخت مشترک ماژول الزام به DbContext دارد، فقط schema اختصاصی `pool` استفاده شود و قرارداد ذخیره‌سازی قبل از افزودن entity تعریف شود؛ مالکیت provider data یا اطلاعات رزرو را نباید بدون نیاز ایجاد کرد.

### API داخلی Refahi

Prefix پیشنهادی و هماهنگ با میزبان فعلی: `/api/pools`. این API نمایندهٔ قرارداد Refahi است و نباید پارامترهای خام PoolTicket را بی‌واسطه به public contract تبدیل کند.

| Route پیشنهادی | کاربرد | نگاشت provider |
|---|---|---|
| `GET /api/pools/states?activeOnly=true` | لیست استان‌ها | `POST /states` |
| `GET /api/pools/cities?stateId={id}&activeOnly=true` | شهرهای استان؛ `stateId` برای همه شهرها اختیاری | `POST /cities` |
| `GET /api/pools?page=1&limit=10&cityId=...` | جست‌وجو بر اساس استان/شهر و فیلترها | `POST /pools/{page}/{limit}` |
| `GET /api/pools?page=1&limit=10&lat=...&lng=...` | جست‌وجوی شعاع/نقشه؛ lat و lng باید با هم بیایند | `POST /pools/{page}/{limit}` |
| `GET /api/pools/{poolId}/saens` | سانس‌های یک استخر | `POST /saens/list` |
| `GET /api/pools/{poolId}/terms-of-use` | شرایط استفاده | `POST /pool/terms-of-use/{poolId}` |
| `POST /api/pools/reservation/calculate` | برآورد قیمت برای ردیف سانس انتخابی | `POST /reservation/calculate` |

در جست‌وجو، `cityId`/`areaId` و `lat`/`lng` باید mutually exclusive اعتبارسنجی شوند. Query endpoint Refahi به body خام provider تبدیل می‌شود و `client_id` فقط در adapter اضافه می‌شود. جزئیات provider مثل `client_id`, `access_token` و `discount_chosen` فقط در صورت نیاز عمومی به قرارداد Refahi راه پیدا کنند.

قرارداد پیشنهادی Calculate ورودی: `saensRowId`, `countAdult`, `countChild`, optional `coupon`. شناسه PoolTicket از نوع عددی است و باید در قرارداد داخلی `long`/نوع متناظر امن نگهداری شود؛ آن را GUID فرض نکنید. پاسخ باید حداقل مجموع بازار و فروش، تعدادها، تاریخ/روز/زمان و تفکیک تخفیف اعلام‌شده توسط provider را منتقل کند. کلیدهای رقم، پول و واحد آن باید از پاسخ واقعی staging تأیید شوند؛ سند واحد پول را صریح نمی‌گوید. تا آن زمان مقدار provider را به **IRR داخلی** تبدیل یا به Wallet ارسال نکنید و label/واحد را در UI جعل نکنید.

همهٔ APIهای کاربری باید از unified `ApiResponse<T>` استفاده کنند. Public discovery قابل استفاده بدون لاگین است؛ عملیات calculate را نیز طبق سند فعلاً read/quote فرض کنید اما اگر PoolTicket رفتار ظرفیت‌گیر دارد باید پیش از انتشار احراز و در rate-limit/idempotency لحاظ شود. عدم وجود purchase API به معنی عدم ساخت Order است؛ از Orders فقط پس از پیاده‌سازی قرارداد واقعی خرید استفاده شود.

### Adapter و عملیات بیرونی

- `PoolTicketOptions` حداقل `BaseUrl`, `ClientId`, `TimeoutSeconds` داشته باشد. `BaseUrl` در محیط deployment روی staging/production تنظیم می‌شود؛ secret از environment/secret store بیاید. هیچ fallback واقعی یا secret در کد نباشد.
- `HttpClient` typed و محدود به دامنهٔ PoolTicket باشد. URLهای provider فقط در Infrastructure ساخته شوند.
- در logها operation، status code، elapsed، outcome و correlation id ثبت شود؛ token، client id، username/password، coupon، دادهٔ هویتی user و payload کامل log نشوند.
- timeout و circuit breaker برای availability provider تعریف شود. به‌دلیل POST بودن عملیات، تا وقتی side-effect نبودن هر عملیات از provider تأیید نشده، retry خودکار نگذارید؛ خطای transport/5xx را با پیام قابل‌فهم و status کنترل‌شده map کنید.
- پاسخ‌هایی با HTTP success ولی `status != 1` به خطای داخلی typed تبدیل شوند. error codeهای شناخته‌شده به خطای validation/not-found/conflict/gateway مناسب map شوند، پیام فارسی داشته باشند و بدنهٔ خام provider به client برنگردد.
- پاسخ‌های upstream را cache نکنید تا سیاست TTL و تازگی قیمت/ظرفیت از provider تأیید شود. دادهٔ جست‌وجو مجوز خرید نیست.

### OAuth PoolTicket

OAuth provider با JWT رفاهی یکی نیست. JWT کاربر Refahi را به PoolTicket access token تبدیل نکنید و token provider را در مرورگر قرار ندهید. endpointهای token/check-token خام را public نکنید.

در فاز discovery، client_id سروری کافی است و access token در جدول پارامترهای PoolTicket اختیاری است؛ پس جریان token لازم نیست شرط نمایش کاتالوگ باشد. اگر coupon یا قابلیت نیازمند user scope لازم شد، پیش از پیاده‌سازی باید روشن شود نام کاربری/گذرواژه PoolTicket چگونه به هویت کاربر Refahi متصل می‌شوند، رمزگذاری password چیست، و token کجا و با چه TTL/encryption ذخیره می‌شود. operator scope باید جداگانه و فقط در ابزار/سطح دسترسی operator استفاده شود. این ابهام‌ها را با رمزگذاری حدسی یا گرفتن password از فرم عمومی حل نکنید.

## معماری و جریان WebApp

در WebApp یک ماژول `Refahi.Clients.Web.Modules.Pool.*` بسازید و API فقط با Backend Refahi ارتباط داشته باشد؛ Base URL و client token PoolTicket در WebApp قرار نگیرد. الگوهای UI و provider-facing query را از Hotels موجود و اجزای design system (`Refahi.Clients.Web.UI`) بازاستفاده کنید؛ layout موازی نسازید.

مسیرهای پیشنهادی (با تأیید route convention نهایی هنگام implementation):

- `/trip/pools`: صفحهٔ جست‌وجو/فهرست استخرها؛ SSR، فیلترها و pagination در query string.
- `/trip/pools/{poolId:long}`: جزئیات استخر، شرایط و سانس‌ها؛ SSR.
- بخش‌های کوچک تعاملی مثل انتخاب شهر/فیلترها/انتخاب سانس و محاسبه قیمت می‌توانند WASM island باشند؛ Island باید `InteractiveWebAssemblyRenderMode(prerender: false)` داشته باشد و به API Refahi متصل شود.
- مسیر خرید/Checkout اضافه نشود تا API رزرو نهایی و قرارداد Orders تأیید شده باشد.

استفاده از SSR برای صفحات discovery/details مطابق Rendering Constitution است. جست‌وجو باید با URL قابل share/reload باشد. برای هر route واقعی، `C:\Workspace\repo\refahiplus-webapp\docs\architecture\page-rendering-inventory.md` به‌روز شود. بخش UI برای loading، خطای سرویس، نبود نتیجه، فروش غیرفعال، سانس کودک/بزرگسال غیرفعال و نمایش توضیح/شرایط HTML provider باید حالت‌های مناسب داشته باشد؛ HTML خارجی را بدون sanitize در DOM قرار ندهید.

اطلاعات قابل نمایش مستند: نام/تصویر/موقعیت/امتیاز/تخفیف/حداقل قیمت استخر؛ نام سانس، قیمت بزرگسال/کودک، تاریخ و ساعت، status فروش، توضیح و شرایط. در UI فقط گزینه‌های قابل فروش فعال باشند؛ فروش کودک و بزرگسال جداگانه از `adult_sale_status` و `child_sale_status` سنجیده شود. برای فیلتر شهر، data از API استان/شهر خود Refahi گرفته شود و mapping خارجی تا وقتی وجود ندارد حدس زده نشود.

## ترتیب پیاده‌سازی پیشنهادی

1. **Foundation Backend:** ایجاد csprojهای پنج‌گانه، ثبت در solution/host، configuration و adapter typed PoolTicket؛ تست قرارداد با `HttpMessageHandler` و نمونه‌های کنترل‌شده.
2. **Discovery API:** states, cities, pools, saens, terms با DTOهای داخلی، validation، error mapping، auth policy و لاگ‌های امن.
3. **Discovery WebApp:** جست‌وجو/لیست و جزئیات SSR با query string، UIهای loading/error/empty و ثبت rendering inventory.
4. **Price calculation:** endpoint و WASM interaction برای انتخاب سانس/تعداد و نمایش پاسخ محاسبه؛ بدون ساخت رزرو/سفارش/پرداخت.
5. **Purchase readiness (مسدود تا قرارداد provider):** دریافت مستند endpointهای create/confirm/status/cancel/refund، semantics ظرفیت و idempotency، مبلغ نهایی و واحد پول، سپس طراحی saga و اتصال به Orders. فقط Orders قابل پرداخت است؛ Wallet مستقیماً با PoolTicket یا Pool module تماس نمی‌گیرد.
6. **OAuth/coupon:** پس از تصویب اتصال هویت Refahi به PoolTicket front scope و تعیین امن password/token lifecycle؛ operator scope جدا از کاربر نهایی.

## قرارداد داده و اعتبارسنجی

- `page >= 1`, `limit` در بازهٔ معقول قابل تنظیم (حداکثر نهایی باید از provider تأیید شود).
- `stateId`, `cityId`, `areaId`, `poolId`, `saensRowId` باید مثبت باشند؛ اگر مرجع‌های upstream قابل تأیید نیستند به جای hardcode مقدار staging در کد، آن‌ها را از پاسخ provider انتخاب کنید.
- `lat` و `lng` هر دو یا هیچ‌کدام باشند و در محدودهٔ جغرافیایی معتبر باشند.
- فیلتر مکانی شهر و مختصات هم‌زمان ممنوع.
- `gender` فقط 0، 1 یا 2.
- `countAdult > 0`, `countChild >= 0`; تعدادها با ظرفیت و وضعیت فروش، مجدداً توسط provider محاسبه/اعتبارسنجی شوند.
- `coupon` optional و trim شده باشد؛ از ثبت آن در log یا URL جلوگیری شود.
- نرخ و قیمت provider با `decimal`/long متناسب قرارداد upstream parse شود؛ پیش از تأیید unit/currency، هیچ تبدیل تومان/ریال فرضی انجام نشود.

## Definition of Done

- پنج پروژه Pool در solution و host ثبت شده‌اند و dependency ruleها نقض نمی‌شوند.
- هیچ لایهٔ WebApp یا module دیگری به Infrastructure/Domain PoolTicket دسترسی مستقیم ندارد.
- secretهای PoolTicket فقط از server-side secret configuration می‌آیند و در repo یا response نیستند.
- endpointهای discovery/calculation دارای validator، CancellationToken، ApiResponseHelper، route name/tag و پیام فارسی‌اند.
- خطای upstream به‌صورت کنترل‌شده map و داده‌های حساس mask می‌شوند؛ retry برای POSTهای تأییدنشده خاموش است.
- WebApp فقط API Refahi را صدا می‌زند؛ discovery SSR/URL-driven و islands با WASM prerender خاموش‌اند؛ inventory به‌روز است.
- تست adapter برای ورودی/خروجی success و همه error codeهای مستند، و تست handler برای validation/mapping اضافه شده است.
- build backend و frontend موفق است. تست‌ها باید برای پیاده‌سازی اضافه شوند؛ در این مرحله هیچ تست یا درخواست زنده‌ای اجرا نشده است.
- خرید/پرداخت تا زمانی که create/confirm/status/cancel/refund و واحد پول upstream مستند نشده، در UI و API غیرفعال می‌ماند.

## ابهام‌های باز برای شروع فاز خرید

1. آیا `reservation/calculate` فقط quote است یا ظرفیت/serial را موقتاً قفل می‌کند؟ مدت اعتبار quote چقدر است؟
2. endpoint ایجاد رزرو، پرداخت/تأیید، استعلام وضعیت، دریافت بلیت/serial، لغو و refund چیست؟ رفتار هرکدام در timeout و درخواست تکراری چیست؟
3. تمام اعداد مبلغ در چه واحدی هستند و coupon/organization discount با قیمت بازار/فروش چطور ترکیب می‌شود؟
4. چه فرمت تاریخ/ساعت و timezone استفاده می‌شود؟ کودک چه شرایط سنی/قیمتی دارد؟
5. password encoding و معنای username در `scope=front` چیست؟ آیا credential به حساب Refahi متصل می‌شود یا PoolTicket account مستقل است؟
6. محدودیت rate، pagination، TTL کاتالوگ/قیمت، سقف تعداد بلیت و قرارداد دقیق GET `/token` چیست؟

این موارد برای فاز discovery مانع نیستند؛ پاسخ آن‌ها پیش‌نیاز هرگونه رزرو قطعی و پرداخت است.
