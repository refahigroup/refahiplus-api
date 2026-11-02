# سناریوی تست دستی لوکال Entertainments / TouristPanel

تاریخ تهیه: ۲۰۲۶-۱۰-۲۷. مبنا: پیاده‌سازی فعلی روی `feat-catalogue-module`.

هدف: کشف خدمت از Entertainments و Catalogue، انتخاب بلیت، رزرو، پرداخت از Checkout مشترک و مشاهده صدور، بدون وابستگی به Commerce.

این سند دستور اجرای تست است؛ تهیه آن به معنی اجرای تست زنده یا موفقیت خرید Provider نیست. راهنمای فنی مکمل: [entertainments-touristpanel.md](entertainments-touristpanel.md).

## ۱. پیش‌نیاز و داده تست

- [ ] هر دو مخزن API و WebApp روی نسخه سازگار باشند؛ تغییرات فعلی این دو مخزن به یکدیگر وابسته‌اند.
- [ ] .NET 10، PostgreSQL و وابستگی‌های فعلی برنامه، از جمله Redis در صورت استفاده تنظیمات محلی، آماده باشند.
- [ ] ConnectionString محیط Development فقط به دیتابیس مشخص تست محلی اشاره کند. Startup برنامه Migrationها را اجرا می‌کند؛ قبل از اجرا، مقصد را بررسی کنید. دیتابیس مشترک یا عملیاتی برای این سناریو مناسب نیست.
- [ ] References شامل استان و شهر فعال باشد. برای سناریوی ترکیبی، Discovery هتل‌ها هم آماده باشد.
- [ ] حساب کاربر A با اطلاعات تماس کامل و موجودی تست مجاز، کاربر B برای کنترل مالکیت و حساب Admin آماده باشند. موجودی از ابزار معتبر فعلی Wallet تأمین شود؛ جدول موجودی مستقیم تغییر نکند.
- [ ] کلیدهای Data Protection بین Restartها حفظ شوند؛ پاک‌کردن Key Ring بازیابی Context رزرو و بلیت را مختل می‌کند.
- [ ] تنظیمات TouristPanel به `Entertainments:Providers:TouristPanel` منتقل شده باشند؛ User Secrets و Environment Variableهای قدیمی Commerce نیز باید اصلاح شوند. رمزها در فایل قابل Commit نوشته نشوند.
- [ ] Media Persistent و AllowedHosts تصاویر تنظیم شده باشند. تصویر ناموفق باید حالت بدون تصویر بدهد، نه حذف خدمت.

**مرز خرید واقعی:** localhost فقط محل اجرای Refahi است. رزرو و صدور می‌توانند در حساب واقعی TouristPanel انجام شوند. بخش خرید این سند فقط با حساب/خدمات تست مورد تأیید Provider و تنظیمات مالی تأییدشده اجرا شود. در غیر این صورت، بخش معرفی و Catalogue اجرا و بخش خرید «اجرا نشده» ثبت شود.

برگه داده را از خروجی واقعی آماده کنید؛ GUID، شهر، قیمت و تاریخ فرضی جایگزین داده واقعی نشوند:

| نام | مقدار موردنیاز |
|---|---|
| P1 | ProgramKey داخلی یک خدمت فعال نوع ۱، نام و SupplierKey آن |
| P2 | ProgramKey داخلی یک خدمت فعال نوع ۲، تاریخ و سانس قابل خرید |
| P3 | خدمت فعال دیگر، ترجیحاً از تأمین‌کننده متفاوت در همان حساب اتصال |
| L1 | کد موقعیت TouristPanel و CityId متناظر فعال References |
| L2 | شهر فعال دیگری که خدمت منتخب در آن قرار ندارد |
| U | در صورت وجود، خدمت منتشرشده با نگاشت جغرافیایی ناقص |
| Q | واژه موجود در عنوان/آدرس/دسته‌بندی خدمت منتخب |

اگر داده‌ای موجود نیست، تست مربوطه «مسدود به دلیل نبود داده» است، نه موفق. ProgramKey عمومی با شناسه بومی TouristPanel یکسان فرض نشود.

## ۲. راه‌اندازی مرحله اول: معرفی با فروش خاموش

در PowerShell اول، از ریشه API اجرا کنید. تنظیم Credentialها باید از قبل در منبع امن تنظیمات قرار داشته باشد:

```powershell
Set-Location C:\Workspace\repo\refahiplus-api
$env:Entertainments__Enabled = 'true'
$env:Entertainments__CatalogueEnabled = 'true'
$env:Entertainments__DiscoveryWorkerEnabled = 'true'
$env:Entertainments__Providers__TouristPanel__Enabled = 'true'
$env:Entertainments__Providers__TouristPanel__SalesEnabled = 'false'
$env:Catalogue__Enabled = 'true'
dotnet run --project src/Refahi.Api --launch-profile 'Refahi.Host (dev)'
```

در PowerShell دوم:

```powershell
Set-Location C:\Workspace\repo\refahiplus-webapp
$env:Entertainments__Enabled = 'true'
dotnet run --project src/Refahi.Clients.Web --launch-profile https
```

آدرس‌های پیش‌فرض تأییدشده در Launch Settings:

- وب: `https://localhost:5271`
- API: `https://localhost:5001`
- Swagger: `https://localhost:5001/api/swagger`

`ApiBaseUrl` محیط Development وب باید همان API باشد. گواهی HTTPS لوکال برای مرورگر و ارتباط SSR معتبر باشد؛ در صورت نیاز از `dotnet dev-certs https --trust` استفاده کنید. برای خطای CORS، AllowedOrigins باید مبدا وب را شامل شود.

API، Schema اختصاصی `entertainments` و Migrationهای آن را در Startup آماده می‌کند. این مرحله داده Commerce/Aabsar را منتقل یا حذف نمی‌کند. Sync اولیه غیرمسدودکننده است؛ Worker هر دقیقه کارهای آماده را بررسی می‌کند و فاصله معمول تازه‌سازی/تلاش مجدد ۱۵ دقیقه است. آماده‌شدن تمام جزئیات ممکن است بیشتر از یک دقیقه طول بکشد.

## ۳. آماده‌سازی معرفی و نگاشت جغرافیا

۱. `GET /api/entertainments/programs?pageNumber=1&pageSize=20` را در Swagger بزنید. قبل از اولین انتشار، حالت آماده‌سازی/عدم دسترسی قابل انتظار است؛ پاسخ ۵۰۳ را با فهرست واقعاً خالی اشتباه نگیرید.

۲. لاگ Sync را بررسی کنید؛ خطای OAuth، قرارداد پاسخ، دیتابیس و Media از هم جدا شوند. پس از انتشار موفق، پاسخ باید شامل معرفی قابل نمایش و ProgramKey پایدار باشد. داده قدیمی مجاز می‌تواند Stale باشد؛ عمر بیشتر از ۷ روز نباید منتشر شود.

۳. با Admin، `GET /api/entertainments/admin/location-mappings` را بخوانید. نگاشت کد دریافت‌شده از Provider را با `PUT` همان مسیر ثبت کنید:

```json
{ "code": "<کد واقعی موقعیت TouristPanel>", "cityId": 123 }
```

عدد ۱۲۳ صرفاً نمونه ساختار است؛ آن را با شناسه شهر فعال واقعی References جایگزین کنید. کد Provider را مستقیماً CityId فرض نکنید. برای مدیریت API از احراز هویت استاندارد Swagger استفاده کنید؛ Token در گزارش تست ثبت نشود.

۴. نگاشت را دوباره بخوانید و فیلتر استان/شهر را تست کنید. شهر غیرفعال یا نامعتبر باید رد شود. درخواست مدیریتی با کاربر عادی باید رد شود؛ ناشناس نیز نباید دسترسی داشته باشد.

**قبولی:** حداقل P1 و P2 معرفی معتبر دارند؛ نگاشت مجموعه تست معلوم است؛ عنوان، متن و تصاویر امن‌اند؛ Sync باعث تغییر هویت برنامه پس از Restart/تازه‌سازی نمی‌شود.

## ۴. تست معرفی و Catalogue، بدون پرداخت

DevTools مرورگر را با Network باز کنید. برای هر مورد نتیجه، زمان و خطای احتمالی را ثبت کنید.

| شناسه | اقدام | انتظار |
|---|---|---|
| D01 | ورود ناشناس به `/entertainments` و سپس جزئیات P1 | فهرست و معرفی دیده شوند؛ نیاز به Login برای مطالعه معرفی نباشد |
| D02 | View Source یا غیرفعال‌کردن JS و Reload جزئیات | معرفی در HTML اولیه باشد؛ انتخاب تعاملی بلیت به JS نیاز دارد |
| D03 | جستجو، فیلتر و صفحه‌بندی در فهرست؛ Refresh و Back/Forward | URL وضعیت را بازسازی کند؛ نتایج مطابق فیلتر باشند |
| D04 | `/catalogue?q=&page=0` | UI صفحه را به ۱ تفسیر کند؛ به علت صفحه صفر فهرست خالی نشود |
| D05 | جستجوی Q در Catalogue | خدمت با دسته‌بندی، محل موجود و لینک پایدار Entertainments دیده شود؛ قیمت Catalogue خالی باشد |
| D06 | دو واژه از دو فیلد متفاوت، مثلاً عنوان و آدرس؛ سپس یک واژه ناموجود | حالت اول تطبیق همه واژه‌ها، حالت دوم بدون نتیجه؛ تطبیق یک واژه کافی نیست |
| D07 | عبارت با ی/ک عربی و فارسی، ارقام و فاصله/نیم‌فاصله | نرمال‌سازی متن رفتار سازگار داشته باشد |
| D08 | فقط استان L1، سپس شهر L1، سپس L2 | خدمت فقط در جغرافیای منطبق حاضر باشد؛ استان بدون شهر قابل انتخاب باشد |
| D09 | U بدون فیلتر و سپس فیلتر شهر | خدمت بدون نگاشت در حالت عمومی حاضر و در فیلتر مکانی غایب باشد |
| D10 | «اطراف من» با اجازه مرورگر | TouristPanel فاصله ساختگی نگیرد؛ در انتهای موارد دارای GIS و با ترتیب عنوان قرار گیرد |
| D11 | رد اجازه موقعیت یا Timeout | پیام قابل فهم و Retry/انتخاب دستی؛ انتخاب قبلی حفظ شود؛ درخواست موقعیت خودکار نباشد |
| D12 | جستجوی مشترک Hotels و Entertainments، صفحه اول و دوم | ترتیب پایدار، بدون تکرار ناشی از ادغام غلط صفحات؛ نقص منبع با پیام Partial مشخص شود |
| D13 | کلیک کارت، ورود مستقیم و Refresh | همان ProgramKey و تأمین‌کننده حفظ شوند |
| D14 | تصویر ناموجود و توضیح دارای HTML در داده تست | بدون تصویر قابل نمایش؛ اسکریپت/HTML خام اجرا نشود |
| D15 | عرض موبایل و دسکتاپ | کارت، فیلتر و BottomSheet بدون اسکرول افقی ناخواسته باشند |

برای ایزوله‌کردن Entertainments از آماده‌نبودن Hotels، API زیر را مستقیم بررسی کنید:

```text
https://localhost:5001/api/catalogue/items?sources=entertainments&page=1&pageSize=20
https://localhost:5001/api/catalogue/filters/locations
```

API Catalogue با `page=0` باید ۴۰۰ بدهد؛ اصلاح صفحه صفر، رفتار UI است. شهر معتبر فاقد پوشش باید نتیجه خالی بدهد؛ شهر نامعتبر ۴۰۰. نبود GIS به تنهایی دلیل Partial نیست.

در Network، مسیر معرفی/جستجوی جدید نباید `/api/commerce` را صدا بزند. درخواست Offers جزئیات از island ممکن است به Provider برسد؛ آن را با خواندن معرفی اشتباه نگیرید. اثبات نبود فراخوانی Provider در Catalogue و پروفایل، از لاگ/Trace سرور انجام شود؛ Network مرورگر ارتباط سرور با Provider را نشان نمی‌دهد. در سرویس نوع ۱، یک ورود عادی به island نباید به علت prerender دو استعلام یکسان تولید کند.

## ۵. فعال‌کردن خرید کنترل‌شده

این مرحله فقط پس از آماده‌بودن داده تست Provider انجام شود. API را متوقف کنید؛ در همان PowerShell:

```powershell
$env:Entertainments__Providers__TouristPanel__SalesEnabled = 'true'
dotnet run --project src/Refahi.Api --launch-profile 'Refahi.Host (dev)'
```

فقط روشن‌کردن Flag کافی نیست. تنظیمات واقعی و تأییدشده حساب باید شامل SettlementConfirmed، BuyPriceConfirmed، PaymentMethod، BankGateway، MarkupPercent، MarkupFixedMinor، DiscountPercent، DiscountFixedMinor، PricingVersion و DeliveryHosts باشد. برای قابلیت‌های مورد استفاده، ManifestConfirmed، ReferenceTicketsConfirmed، DateRangeConfirmed و TimeZoneId نیز مطابق قرارداد فعلی بررسی شوند. اعداد مالی و Enumها حدس زده نشوند؛ صفر سود هم باید صریح تنظیم شود.

برای هر دور، موجودی اولیه Wallet و زمان شروع را ثبت کنید. واحد مبلغ API برابر IRR است؛ اگر UI تبدیل نمایشی دارد آن را لحاظ کنید.

## ۶. مسیر طلایی خرید، دوبار برای نوع ۱ و نوع ۲

### دور A: خدمت نوع ۱

۱. ناشناس روی افزودن بلیت P1 بزنید؛ Login درخواست شود و پیش از احراز هویت Mutation سبد انجام نشود. با کاربر A وارد شوید.

۲. نوع بلیت و تعداد ۲ را انتخاب و به سبد اضافه کنید. `/entertainments/cart` باید برنامه، تأمین‌کننده، نوع بلیت، تعداد و قیمت نمایشی صحیح داشته باشد. تغییر تعداد و حذف/افزودن را یک بار امتحان کنید. نوع ۱ نباید تاریخ/سانس ساختگی لازم داشته باشد.

۳. یک قلم P3 از تأمین‌کننده دیگر همان حساب اضافه کنید، اگر داده موجود است. هر دو باید در سبد و رزرو نهایی حفظ شوند.

۴. وارد `/entertainments/checkout` شوید. اطلاعات تماس باید صحیح باشد. فقط مشخصات مسافر موردنیاز همان برنامه درخواست شود؛ برای دو بلیت، الزامات دو مسافر رعایت شود. ورودی ناقص باید پیش از رزرو پیام اصلاح بدهد.

۵. «رزرو و مشاهده مبلغ نهایی» را بزنید. نتیجه Reserved، مبلغ واقعی و مهلت پرداخت را ثبت کنید. تا قبل از تأیید، پرداخت و صدور انجام نشود. مشخصات مسافر نباید در URL یا Storage باشد؛ فقط کلید/شناسه غیرحساس بازیابی نشست مجاز است.

۶. صفحه را Refresh کنید. همان نشست و مبلغ از سرور بازیابی شود؛ رزرو دوم ساخته نشود. برای رفع ابهام شبکه، کلید یکتایی همان تلاش حفظ شود.

۷. «تأیید مبلغ و ادامه پرداخت» را بزنید. باید Checkout مشترک `/checkout/orders/{OrderId}` باز شود. PurchaseId، SessionId و OrderId را جدا ثبت کنید. Order دارای SourceModule برابر Entertainments و ReferenceType برابر EntertainmentPurchase باشد. مبلغ Order با مبلغ تأییدشده یکسان باشد.

۸. با Wallet تست مجاز پرداخت کنید. تنها Order پرداخت شود؛ یک کسر متناظر در دفتر Wallet ایجاد شود. بازگشت باید به `/entertainments/purchases/{PurchaseId}` باشد.

۹. وضعیت پرداخت و صدور جدا نمایش داده شوند. توالی ممکن است آن‌قدر سریع باشد که تمام حالت‌های میانی دیده نشوند. پایان موفق باید Completed، تعداد صحیح بلیت و اطلاعات تحویل معتبر داشته باشد. کد/QR بلیت و لینک محرمانه را در گزارش عمومی نگذارید.

۱۰. Refresh، بازگشت از Profile و بازکردن دوباره Purchase باید همان خرید را نشان دهد؛ کسر Wallet یا صدور دوباره رخ ندهد. کارت Order در Profile باید به جزئیات Entertainments برسد.

### دور B: خدمت نوع ۲

همان مراحل را برای P2 تکرار کنید، با این تفاوت‌ها:

- تاریخ مجاز، سانس مشخص و نوع بلیت انتخاب شوند؛ بدون انتخاب لازم، افزودن معتبر نباشد.
- تاریخ، سانس، تعداد و مسافران در سبد، نشست، Order Snapshot و بلیت نهایی با انتخاب اولیه تطبیق داشته باشند.
- تغییر سانس/تاریخ، همان انتخاب قبلی فرض نشود؛ رزرو جدید پس از بازبینی لازم است.
- اگر Provider در رزرو قیمت را تغییر داد، مبلغ واقعی باید پیش از پرداخت بازبینی شود؛ پرداخت بی‌صدای مبلغ تغییرکرده مردود است.

**قبولی هر دور:** یک نشست تأییدشده، یک Purchase، یک Order قابل پرداخت، یک پرداخت و صدور سازگار با اقلام؛ صفر درخواست Commerce در جریان جدید.

## ۷. خطاها، یکتایی و دسترسی

| شناسه | اقدام کنترل‌شده | انتظار |
|---|---|---|
| E01 | قبل از تأیید، رزرو را با دکمه لغو آزاد کنید | بازگشت به سبد، بدون Order پرداخت‌شده و بدون کسر Wallet |
| E02 | یک رزرو تست را تا پایان مهلت رها کنید | پرداخت نشست منقضی ممنوع؛ تاریخ یا رزرو تازه خودکار ساخته نشود |
| E03 | سبد را در تب دوم تغییر دهید و از تب قدیمی رزرو کنید | نسخه قدیمی رد شود یا بازبینی لازم شود؛ خرید انتخاب ناخواسته رخ ندهد |
| E04 | تأیید یک نشست را دوباره با همان ورودی ارسال کنید | Order/Purchase دوم ساخته نشود؛ پاسخ بازیابی‌پذیر باشد |
| E05 | همان IdempotencyKey را با ورودی متفاوت استفاده کنید | تعارض؛ رزرو یا خرید متفاوت زیر همان کلید ایجاد نشود |
| E06 | با کاربر B، Session/Purchase/بلیت کاربر A را بخوانید | دسترسی رد شود؛ هیچ داده خصوصی برنگردد |
| E07 | با کاربر عادی Admin API را فراخوانی کنید | دسترسی رد شود |
| E08 | فروش را خاموش و API را Restart کنید | فروش جدید بسته شود؛ پیگیری خرید قبلاً پرداخت‌شده ادامه داشته باشد |
| E09 | API عمومی را با Enabled=false و Restart ببندید | ورودی عمومی بسته؛ مسیرهای ادامه نشست/خرید و عملیات مرتبط برای مالک/ادمین قابل استفاده بمانند |

E04 و E05 را با درخواست‌های خودتان در ابزار API و حفظ همان شناسه‌ها انجام دهید؛ کلید تازه برای شبیه‌سازی Retry نسازید. هنگام اشتراک گزارش، بدنه مسافران و Authorization حذف شوند.

### خطای نامشخص و بازیابی پیشرفته

این بخش به Fixture یا محیط تست کنترل‌شده Provider نیاز دارد. قطع اینترنت یا کشتن فرایند وسط صدور حساب واقعی، روش تست عمومی این سند نیست.

- پاسخ رزرو گم شود: همان کلید از `/checkout/sessions/by-key?key=...` بازیابی شود؛ رزرو تازه ایجاد نشود.
- نتیجه صدور نامشخص شود: AwaitingResult یا ManualReview، بدون Finalize دوم و بدون Refund خودکار.
- Invoice معتبر موجود باشد: Admin از `/admin/operations/{id}/recheck` فقط استعلام انجام دهد؛ خرید جدید نسازد.
- Invoice موجود نباشد یا صدور جزئی/مغایر باشد: بررسی دستی؛ Pending به معنی عدم صدور قطعی نیست.
- پاسخ `resolve` فقط با نتیجه مستند Provider، مدرک، هویت اپراتور و کلید یکتایی ثبت شود؛ برای عبور تست، نتیجه ساختگی fulfilled/notcreated ثبت نشود.
- رزرو پرداخت‌نشده نامشخص از `/admin/reservations` پیگیری شود؛ resolve آن نباید Purchase متصل را دور بزند یا Refund بسازد.
- عدم صدور قطعی، جبران را از Orders آغاز کند؛ کسر و بازگشت Wallet باید یک‌بار و با Allocation اصلی تطبیق داشته باشند.
- لغو خودکار بلیت صادرشده TouristPanel در دامنه قابلیت نیست؛ انتظار Refund خودکار آن نداشته باشید.

## ۸. رگرسیون و پایان تست

- [ ] لینک قدیمی عمومی TouristPanel به برنامه/تأمین‌کننده جدید برسد؛ تاریخ معتبر حفظ شود و شناسه ناشناخته ۴۰۴ کنترل‌شده بدهد.
- [ ] Commerce/Aabsar همچنان قابل مرور و استفاده باشد؛ داده‌ها و سفارش‌های قبلی آن تغییر نکنند.
- [ ] Hotels، Catalogue، Checkout و Profile مسیرهای قبلی را حفظ کنند.
- [ ] در Application/Service Worker مرورگر، نسخه تازه WASM بارگیری شود. API از Cache پاسخ نگیرد؛ از نسخه قدیمی PWA نتیجه‌گیری نشود. بررسی Worker انتشار را روی خروجی Publish انجام دهید؛ Development الزاماً Worker فعال انتشار ندارد.
- [ ] پاسخ خصوصی API و APIهای جدید no-store داشته باشند؛ خطاهای خام Provider، Credential، هزینه داخلی Provider و اطلاعات تسویه در پاسخ عمومی نباشند.
- [ ] هنگام خروج از صفحه Purchase، Polling متوقف/محدود شود؛ با ورود دوباره وضعیت از سرور بازخوانی شود.
- [ ] پس از تست، فروش جدید خاموش شود؛ تا تعیین تکلیف رزروها/خریدهای باز، نسخه سازگار، Credentialها و پردازش پیگیری باقی بمانند. جدول‌ها و Key Ring حذف نشوند.

Environment Variableهای نمونه فقط به همان PowerShell و فرایندهای فرزند اعمال می‌شوند. برای پایان، فرایندها را متوقف و ترمینال‌های تست را ببندید؛ فایل تنظیمات یا Secret Store‌ای که دستی تغییر کرده باید جدا به مقدار قبلی بازگردد.

## ۹. قالب ثبت نتیجه و معیار قبولی

برای هر ردیف ثبت کنید:

```text
Test ID:
نسخه/Commit API و WebApp:
زمان و محیط:
داده تست و ProgramKey:
نتیجه: موفق / ناموفق / اجرا نشده / مسدود
انتظار و رفتار مشاهده‌شده:
SessionId / PurchaseId / OrderId / شناسه عملیات در صورت نیاز:
HTTP status و پیام امن خطا:
مدرک بدون Token، مشخصات مسافر، QR یا لینک محرمانه:
```

قبولی تست لوکال کامل: معرفی و Catalogue صحیح، هر دو نوع خدمت تا پایان صدور آزموده، یکتایی/مالکیت و بازگشت Checkout تأیید، و رگرسیون‌های مربوط موفق باشند. بخش‌های فاقد داده یا حساب تست باید صریح «اجرا نشده/مسدود» بمانند. موفقیت لوکال جای تأیید قرارداد زنده Provider و بارسنجی انتشار را نمی‌گیرد.


## انتخاب خودکار قیمت و کسورات

- بدون تعریف AllowedPriceCategoryIds، بلیط دارای کمترین بهای تأمین مثبت ریالی نمایش داده شود؛ عنوان ردهٔ کمیسیون در UI نیاید.
- گزینهٔ بدون قیمت، غیرفعال یا بدون ظرفیت، وضعیت فارسی داشته باشد؛ انتخاب‌گر خالی و قیمت صفر به‌عنوان رایگان نمایش داده نشود.
- با یک گزینهٔ قابل خرید، انتخاب‌گر نوع بلیط حذف و همان گزینه انتخاب شود. برای تاریخ بدون سانس و خطای شبکه، پیام‌های جدا و تلاش مجدد بررسی شوند.
- تغییر قیمت هنگام افزودن به سبد، مبلغ جدید را نمایش دهد و کلیک مجدد بخواهد؛ درخواست افزودن خودکار تکرار نشود.
- با یک ردیف سبد قدیمی و افزودن همان نوع بلیط با کلید جدید، فقط یک ردیف بماند. ردیف غیرقابل بازیابی بدون حذف خودکار پیام انتخاب مجدد بدهد.
- کسورات درصدی و ثابت از بهای تأمین محاسبه شوند. مبلغ نهایی مثبت زیر بهای تأمین مجاز، و مبلغ نهایی صفر یا منفی غیرقابل خرید باشد.
- پس از رزرو، تغییر تنظیمات قیمت یا ردهٔ ارزان‌تر نباید مبلغ رزرو یا سفارش را عوض کند. با تغییر قاعده، PricingVersion را تغییر دهید.
- برای دستهٔ بدون نگاشت، CategoryCode برابر entertainment باشد و محدودیت کیف‌پول همچنان اعمال شود.
- فعال‌سازی فروش واقعی جداگانه و پس از تکمیل تأییدهای قرارداد و تسویه انجام شود؛ تست خواندنی، تأیید خرید واقعی نیست.

## Entertainment content and customer boundary — 2026-11-01

Accepted ADR: Catalogue continues federated public discovery; Entertainment owns local specialized content, offers and purchases. Content handlers use IEntertainmentContentReader. IEntertainmentDiscovery, IEntertainmentProgramIdentity and IEntertainmentLocationMappings have separate contracts. Sales adapters no longer expose content listing/filter methods. No Commerce dependency is introduced.

Customer content omits intermediary names. SupplierKey is now an opaque GUID backed by entertainments.touristpanel_suppliers; public supplier routes are /entertainments/suppliers/{SupplierId:guid} and /programs. The additive EntertainmentPublicContentAndDelivery migration backfills supplier identities, normalized SQL ordering and queues profile refresh. ProgramKey, purchase/order IDs, option identities, historical encrypted JSON and financial amounts are preserved.

Cart mutations accept ProgramKey and the selected offer/option. Checkout accepts CartItemIds, CartVersion, passengers and the existing idempotency key; ownership and the complete provider group are validated on the server. Customer cart exposes CheckoutGroups without provider identifiers. The browser stores selection and recovery keys in user-scoped sessionStorage. Existing checkout keys are recovered before starting another reservation. Deploy backend and frontend together: previous provider-scoped customer contracts are removed in this release.

Cart request regression: `POST /api/entertainments/cart/items` accepts ProgramKey in both the 32-character format returned by offers and the hyphenated GUID format used in routes. Invalid, missing and empty GUID identities must fail FluentValidation with a Persian message before identity lookup or provider calls. Verify both valid formats against the same program/offer/option and current expected price; JSON binding must not return 400 for the compact format. Automated coverage: `CartRequestBindingTests`.

Purchase responses exclude external invoice identifiers, documents, provider keys and raw failures. DeliveryStatus distinguishes Ready, Pending and NeedsReview independently of financial/issuance state. Only authoritative admission numbers or non-URL QR payloads are shown; an internal ticket ID is never an entry code. Original provider documents remain encrypted internally. Missing credentials set DeliveryNeedsReview and appear in the admin operations queue; this never triggers another issuance or automatic refund. The existing Refahi purchase page renders/prints the ticket locally using QRCoder. Operators must verify admission with the provider before treating document-only delivery as usable.

Public Commerce links for unregistered providers return 404 with no redirect. Legacy Entertainment native-ID resolution is removed. Commerce workers filter registered provider keys before batch limits; Aabsar and historical migration archives remain. No production/test application database or live provider reservation was changed by this refactor. Inventory and operator resolution of any external historical TEST reservations remain deployment work.

Release gates: provider-approved service-type 1/2 purchases, actual venue admission with Refahi ticket credentials, review of externally supplied imagery/content for branding, and live volume checks. Do not enable public sales for document-only or externally navigated QR delivery. The referenced TouristPanel-Production-Implementation.md remains unavailable. Maintain the DataProtection key ring and compatible workers on rollback; disable new sales first.
