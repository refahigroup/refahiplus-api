# هتل لاماسو در کنار SnappTrip

## دامنه و وضعیت

این تغییر فقط Hotels را در پنج لایه موجود توسعه می‌دهد. APIهای عددی قدیمی SnappTrip باقی می‌مانند؛ WebApp برای جست‌وجو و انتخاب جدید از `/api/hotels/v2` استفاده می‌کند. پرواز، تطبیق هتل‌ها، حذف تکرار و انتخاب ارزان‌ترین منبع خارج از این نسخه‌اند.

جست‌وجوی دو پرووایدر مستقل و هم‌زمان است. هر نتیجه یک HotelKey مستقل دارد، حتی وقتی شناسه یا نام هتل در دو منبع یکسان باشد. فیلتر و مرتب‌سازی پس از دریافت نتایج اعمال می‌شوند. شکست یک منبع پاسخ ناقص با پیام عمومی می‌دهد؛ شکست همه منابعِ قابل جست‌وجو 503 است. نبود نگاشت شهر، پاسخ سالم خالی محسوب نمی‌شود.

خرید واقعی لاماسو فعال نشده است. مستند عمومی [لاماسو](https://docs.lamasoo.com/) و کالکشن منتشرشده آن (2024-11-05) Inventory، Cities، Availability، Submit، Confirm و Resubmit دارند؛ قرارداد قابل اتکای inquiry، cancellation، voucher و wallet reconciliation ندارند. قابلیت Purchase تا وجود آداپتر واقعی و تأییدشده چرخه‌عمر بسته است؛ تغییر `PurchaseEnabled` یا `ContractVerified` به‌تنهایی آن را فعال نمی‌کند. ابتدا باید قرارداد و adapterهای واقعی بازیابی/کنسلی و تأمین از کیف پول خارجی تکمیل و تست شوند. Endpoint فرضی ساخته نشده و Resubmit خودکار وجود ندارد.

## قرارداد و مالکیت داده

- `ReferenceCityId` شناسه عددی شهر فعال رفاهی و `HotelKey` شناسه GUID هتل است. نگاشت بومی هر پرووایدر از جدول اختصاصی V4 خوانده می‌شود؛ CityKey تاریخی منبع جستجو نیست.
- شناسه اصلی هتل، اتاق، نرخ و واسطه رشته‌ای است. داده تاریخی عددی حذف نشده است.
- OfferToken تصادفی 192بیتی فقط Snapshot سمت سرور را معرفی می‌کند. قیمت، پرووایدر و RatePlan مرورگر مرجع خرید نیستند.
- Snapshot پیشنهاد تاریخ انقضای 20دقیقه‌ای دارد؛ قیمت پیش از ثبت درخواست دوباره و بدون Cache استعلام می‌شود. تغییر سن کودک نیاز به quote تازه دارد.
- تخصیص بزرگسالان/سن کودکان به اتاق‌ها در URL و API حفظ می‌شود. نام، کد ملی، تماس و مشخصات هویتی وارد URL نمی‌شوند.
- `HotelRequest` مالک رزرو است؛ تنها Orders پرداخت می‌شود. Wallets داخلی مستقیماً با لاماسو ارتباط ندارد.

| مسیر | رفتار |
|---|---|
| GET `/api/hotels/v2/cities?name=...` | شهر فعال مرجع: referenceCityId، name، nameEn، provinceId، provinceName |
| GET `/api/hotels/v2/availability/city` | referenceCityId، checkin/checkout، parties JSON، minPrice/maxPrice به ریال، stars تکراری، accommodations تکراری، sort=price/price-desc/stars |
| GET `/api/hotels/v2/{hotelKey}` | جزئیات و پیشنهادها با همان منبع و شرایط سفر |
| GET `/api/hotels/v2/offers/{token}` | خواندن پیشنهاد معتبر |
| POST `/api/hotels/v2/offers/{token}/quote` | بدنه HotelTravel؛ پیشنهاد تازه برای ترکیب فعلی |
| POST `/api/hotels/v2/requests` | ورود کاربر، OfferToken، HotelTravel، Guests با RoomIndex، تماس، ExpectedTotalPriceMinor و هدر Idempotency-Key |

پاسخ‌ها یکپارچه، پیام‌ها فارسی و پاسخ‌های v2 دارای `Cache-Control: no-store` هستند. هزینه تأمین و کارمزد مستقل از قیمت فروش ثبت می‌شوند. `IHotelSalePriceCalculator.CalculateFinalPrice` فعلاً قیمت نمایشی کل اقامت را بدون افزایش برمی‌گرداند. تمام مبالغ داخلی long و IRR هستند؛ تبدیل تومان فقط در مرز SnappTrip و نمایش انجام می‌شود.

## جریان تراکنش و بازیابی

برای لاماسو: quote تازه ← ذخیره درخواست، Saga و reference_id پایدار ← ذخیره Submitting ← Submit ← ثبت هزینه و قفل ← سفارش با مهلت قفل منهای 60ثانیه ← Checkout موجود ← پرداخت Orders/Wallets ← ذخیره Confirming ← Confirm ← ثبت reservation_id نهایی.

برای SnappTrip، رزرو پس از پرداخت از مسیر موجود ادامه دارد. مشخصات انتخاب و ترکیب هر اتاق در Snapshotهای v2 باقی می‌مانند؛ آرایه اتاق‌ها و نام/نام خانوادگی مستقل به درخواست خرید منتقل می‌شوند. لینک‌ها و درخواست‌های قدیمی رفتار سازگار خود را حفظ می‌کنند. Fingerprint قدیمی رزرو نیز تغییر نکرده است.

کلید یکسان با داده متفاوت 409 می‌دهد. درخواست ثبت‌شده، payload و کلید تلاش مجدد در sessionStorageِ همان کاربر/پیشنهاد نگهداری می‌شوند؛ پس از ثبت موفق شناسه درخواست نیز ذخیره می‌شود تا شکست تبدیل به سفارش باعث ثبت درخواست تازه نشود. کلید ایجاد Order از شناسه HotelRequest مشتق می‌شود.

Submit/Confirm لاماسو Retry خودکار ندارند. Retry عملیات خرید SnappTrip در HttpClient نیز حذف شده است. Timeout، پاسخ 5xx/408 یا پاسخ موفق غیرقابل تفسیر در عملیات تغییر وضعیت، Unknown می‌شود. وضعیت نامعلوم باعث رزرو مجدد، پرداخت یا Refund خودکار نمی‌شود. رزرو خارجی پرداخت‌شده تا اثبات وضعیت کنسلی/بازگشت اعتبار، اجازه جبران مالی خودکار ندارد. هر جبران داخلی همچنان از Orders و allocation اولیه انجام می‌شود.

قفل‌های advisory PostgreSQL، هم‌زمانی میان پردازش‌ها را کنترل می‌کنند؛ هنگام HTTP تراکنش باز EF نگه داشته نمی‌شود. ثبت نتیجه جست‌وجو ترتیبی است. Inventory از DbContext مستقل در scope جداگانه استفاده می‌کند تا اجرای موازی پرووایدرها DbContext مشترک را درگیر نکند. Version رزرو خارجی concurrency token است.

Worker عملیات رهاشده Submitting/Confirming را Unknown می‌کند، رزروهای پرداخت‌شده با قفل معتبر را برای Confirm بررسی می‌کند و موارد منقضی/نامعلوم را از پرداخت خارج می‌کند. بدون inquiry معتبر، Unknown با حدس یا شمارش تعداد تلاش به Failed تبدیل نمی‌شود. پیگیری عملیاتی از `hotels.external_reservations` و لاگ‌های correlation انجام می‌شود؛ اطلاعات هویتی و payload خام در لاگ نوشته نمی‌شوند. به‌روزرسانی مستقیم SQL وضعیت مالی روش بازیابی مجاز نیست؛ قرارداد تأمین‌کننده و رویه عملیاتی باید قبل از فعال‌سازی خرید تکمیل شوند.

## تنظیمات و انتشار

تنظیمات در `Hotels:Providers:Lamasoo` تعریف شده‌اند. کلید از `REFAHIPLUS_LAMASOO_API_KEY` تأمین می‌شود؛ secret واقعی در repository نیست.

```json
{
  "SearchEnabled": false,
  "PurchaseEnabled": false,
  "ContractVerified": false,
  "IncludeAgencyProvider": false,
  "TimeoutSeconds": 8
}
```

نگاشت‌های زمان اجرا از جدول‌های hotels.snapptrip_city_mappings و hotels.lamaso_city_mappings خوانده می‌شوند؛ IsEnabled لاماسو رعایت می‌شود. تنظیمات CityMappings/CityCountries/CityCodes دیگر مصرف نمی‌شوند. برای قرارداد مرجع، migration و ترتیب انتشار، [راهنمای شهر مرجع](hotels-reference-city-search.md) را بخوانید.

Availability حداکثر 60ثانیه و با کلید کامل شرایط سفر در حافظه Cache می‌شود. Inventory و تصاویر در جدول provider_inventory نگهداری و با اولین دسترسی پس از 24ساعت تازه می‌شوند. فهرست شهرها مستقیماً از مرجع رفاهی و نگاشت‌ها بدون Cache از جداول V4 خوانده می‌شوند. Circuit Breaker هر منبع مستقل است. SnappTrip نیز `SearchEnabled`، `PurchaseEnabled` و `SearchTimeoutSeconds` مستقل دارد؛ خرید جدید غیرفعال شود، مسیر پیگیری رزروهای قبلی همچنان قابل استفاده است.

دو Migration افزایشی در schema hotels اضافه شده‌اند:

1. `HotelsLamasooCatalogue`: ارجاعات شهر/هتل، پیشنهادها، رزرو خارجی و ستون‌های جدید اختیاری HotelRequest.
2. `HotelsProviderInventory`: کاتالوگ پایدار Inventory.

Migration در این جلسه روی دیتابیس اجرا نشده است. ابتدا backup و SQL migration را در staging بررسی کنید، سپس Backend جدید و WebApp را منتشر کنید. APIهای قدیمی باقی می‌مانند و ستون تاریخی حذف نمی‌شود. SearchEnabled لاماسو را پس از تنظیم credential و فهرست شهرهای تأییدشده در محیط آزمایشی فعال کنید؛ CanBook لاماسو همچنان false می‌ماند. rollback عملیاتی با غیرفعال کردن جست‌وجوی لاماسو انجام می‌شود، بدون حذف داده یا توقف پیگیری رزروهای قبلی.

قبل از فعال‌سازی Purchase باید رفتار reference_id تکراری، inquiry بعد از Timeout، برداشت کیف پول خارجی، refund، voucher، چند اتاق/کودک/تخت اضافه، کارمزد و گردکردن، محدودیت درخواست‌ها و الزامات customer با نمونه تأییدشده تأمین‌کننده آزموده شوند. Fixture عمومی جای خرید آزمایشی با credential واقعی را نمی‌گیرد.

## WebApp و آزمون

صفحات `/trip/hotels/search` و `/trip/hotels/{hotelKey:guid}` SSR هستند؛ کارت‌های نتیجه در HTML اولیه نمایش داده می‌شوند. فرم جست‌وجو و کنترل‌ها WASM island هستند. `/trip/hotels/checkout` و ادامه درخواست WASM با prerender:false باقی می‌مانند. Layout و مسیر Checkout دوم ایجاد نشده است. نام پرووایدر از بخش نمایشی سفارش حذف شده است. Rendering Inventory در repository WebApp و مستند مشترک به مسیرهای واقعی اصلاح شده است.

Service Worker بررسی شد: API network-only و navigation network-first است. داده Cache‌شده مرورگر مجوز پرداخت نیست؛ اعتبار پیشنهاد و رزرو در سرور بررسی می‌شود.

تست‌های Hotels، Orders و Wallets باید همراه بیلد کامل هر دو Solution اجرا شوند. تست‌های جدید شامل تکرار نتایج، مسیریابی جزئیات، اختلال جزئی/کامل، Cache با سن کودک، quote تازه، قیمت روزانه در برابر مجموع، ظرفیت/قوانین اقامت، idempotency، ذخیره قبل از Submit، Timeout بدون ارسال مجدد/Refund، Confirm تکراری، کنترل مالکیت/مبلغ پرداخت و تخصیص اتاق‌های SnappTrip هستند. تست HTTP با Fixture انجام می‌شود؛ آزمون قرارداد زنده و بررسی موبایل با credential واقعی هنوز شرط انتشار خرید است.


## نتیجه بررسی محلی

- بیلد کامل Backend و WebApp موفق بود. هشدارهای nullable قبلی Backend باقی‌اند.
- تست‌های Hotels، Orders و Wallets و تست‌های جدید WebApp Hotels اجرا شدند.
- با API Fixture و اجرای واقعی WebApp، جست‌وجو بدون city-key پاسخ 200 داد؛ دو لینک مستقل هتل مشترک و پیام پاسخ ناقص در HTML اولیه موجود بود. جزئیات GUID نیز پیشنهاد، توکن انتخاب و ساعت ورود را در HTML اولیه نمایش داد.
- EF گزارش کرد Model و Migration Snapshot هماهنگ‌اند. دیتابیس و سرویس واقعی لاماسو در این آزمون تغییر نکردند.
- Meter با نام `Refahi.Hotels.Providers`، شمار درخواست و مدت عملیات را با برچسب provider/operation/outcome منتشر می‌کند. collector محیط بهره‌برداری باید این Meter را دریافت کند؛ نام مسافر، توکن پیشنهاد و شناسه‌های پرتنوع برچسب متریک نیستند.


## تکمیل چرخه خرید — ۲۰۲۶-۱۰-۲۰

مدل تلاش خارجی، Migration محافظه‌کارانه به lamaso_reservations، API پیگیری درخواست، کنترل زیان، بازیابی و تفکیک پرداخت از رزرو در کلاینت اضافه شدند. خرید واقعی همچنان به قرارداد تکمیلی وابسته است. جزئیات رفتار، تنظیمات، انتشار و محدودیت‌ها در [سند چرخه خرید](hotels-lamaso-booking-lifecycle.md) ثبت شده‌اند. Migration این مرحله روی PostgreSQL دورریختنی آزموده شده است؛ production تغییر نکرده است.
