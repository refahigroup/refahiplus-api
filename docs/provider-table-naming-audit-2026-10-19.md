# بررسی مالکیت و نام‌گذاری جدول‌های پرووایدر — ۲۰۲۶-۱۰-۱۹

دامنه بررسی: DbContextها، Entity Configurationها، مدل‌های مرتبط، SQLهای import و مسیرهای دسترسی Backend در checkout فعلی مخزن. این بررسی ساختار کد است؛ فهرست جدول‌های دیتابیس اصلی مستقیماً خوانده نشده است. Migrationهای تاریخی و Fixtureهای ارتقا، تعریف ساختار فعال محسوب نشده‌اند. این موارد بدهی معماری نسبت به قانون جدید هستند، نه اثبات خرابی جاری سرویس.

## قانون ثبت‌شده

داده اختصاصی پرووایدر در schema ماژول مصرف‌کننده و با پیشوند پایدار همان پرووایدر ذخیره شود؛ مثلاً `hotels.lamaso_*` و `hotels.snapptrip_*`. کشور/استان/شهر مرجع، مالکیت References دارد. سفارش، پرداخت، درخواست، Saga و سایر داده‌های کسب‌وکار مشترک صرف داشتن ProviderId به جدول اختصاصی پرووایدر تبدیل نمی‌شوند. Payload و state اختصاصی بیرونی که داخل آن‌ها جاسازی شده باید برای استخراج بررسی شود.

قانون در بخش ۳۱ `AGENTS.md` ثبت شد. پیشوند SQL `lamaso` است؛ الزاماً نیازی به تغییر enum/نام قرارداد بیرونی `Lamasoo` یا شناسه تاریخی پرووایدر نیست.

## موارد نیازمند تغییر

| اولویت | جدول/محل فعلی | شاهد در کد | اصلاح پیشنهادی |
|---|---|---|---|
| ۱ | `references.provider_city_mappings`، `references.provider_province_mappings` | اسکریپت V1 نگاشت همین کار؛ نگاشت native مقصد هتل به مرجع است، نه خود مرجع | در V2 به چهار جدول `hotels.snapptrip_city_mappings`، `hotels.snapptrip_province_mappings`، `hotels.lamaso_city_mappings` و `hotels.lamaso_province_mappings` تفکیک شده است. اگر V1 اجرا شده باشد، انتقال داده و حذف بدون CASCADE جدول قبلی انجام می‌شود. |
| ۱ | `hotels.hotel_provider_booking_cache` | `HotelProviderBookingCacheEntryEntityTypeConfiguration.cs:13`؛ مدل `ProviderName`، پاسخ JSON، کد رزرو خارجی و وضعیت عملیات دارد | داده با ProviderName تفکیک و به `hotels.snapptrip_booking_operations` و جدول متناظر هر پرووایدر منتقل شود. Repository/Finalize/Recovery/Cancel باید از همان provider ذخیره‌شده مسیریابی شوند؛ حفظ idempotency و Unknown الزامی است. |
| ۱ | `flights.flight_search_offer_snapshots` | `FlightOfferSnapshotConfiguration.cs:11`؛ `ProviderName`، `ProviderFareSourceCode`، `ProviderSearchId`، `ProviderSnapshotJson` | بخش پیشنهاد عمومی و OfferToken می‌تواند مشترک بماند؛ payload و شناسه‌های native به `flights.snapptrip_offer_snapshots` و جدول‌های پرووایدرهای بعدی منتقل شوند. |
| ۱ | `commerce.catalog_snapshots` | `CommerceDbContext.cs:33` و `CommerceCatalogSnapshot.PayloadJson`؛ snapshot متعلق به account خارجی | `commerce.touristpanel_catalog_snapshots` و جدول مستقل سایر adapterها؛ account_key/credentials identity، زمان انتشار و cache invalidation حفظ شود. |
| ۱ | `commerce.provider_receipts` | `CommerceDbContext.cs:35`؛ `CommerceProviderReceipt` پاسخ رمز‌شده بیرونی را پیش از نگاشت کسب‌وکار ذخیره می‌کند | جدول‌های `commerce.touristpanel_receipts` و `commerce.aabsar_receipts` حسب مالک واقعی رکورد. بدون تشخیص provider از account/config، رکوردها تقسیم نشوند. کلید OperationId+AccountKey و ترتیب ذخیره checkpoint حفظ شود. |
| ۲ | `commerce.provider_fulfillments`، `commerce.provider_tickets`، `commerce.provider_operation_attempts` | `CommerceDbContext.cs:76/86/91`؛ state خرید بیرونی، کد تیکت، payload hash و idempotency عملیات | قرارداد مشترک fulfilment می‌تواند باقی بماند؛ جزئیات native و عملیات هر provider به `<provider>_fulfillments/tickets/operation_attempts` منتقل شوند. ProviderKeyهای واقعی فعلی `touristpanel` و `aabsar` هستند. انتقال یکپارچه تمام جدول به نام یک provider غلط است. |
| ۱ | `charge.charge_fulfillment_attempts` | `ChargeFulfillmentAttemptConfiguration.cs:12`؛ ستون اختصاصی `eniac_result_code` در کنار request/response snapshot | استخراج به `charge.eniac_fulfillment_attempts`؛ وضعیت سفارش/درخواست شارژ مشترک باقی بماند. ارتباط ProviderCallLogId و رفتار Recovery حفظ شود. |
| ۲ | `charge.provider_call_logs` | `ProviderCallLogConfiguration.cs:11`؛ ProviderName و بدنه درخواست/پاسخ بیرونی | payload لاگ Eniac به `charge.eniac_call_logs`؛ اگر رخداد عمومی observability مشترک باقی می‌ماند، فقط metadata مشترک نگه دارد و به رکورد اختصاصی اشاره کند. |
| ۲ | `cinema.provider_attempts` | `CinemaDbContext.cs:22`؛ عملیات و نتیجه تماس پرووایدر از سفارش والد به ارث می‌رسد؛ provider فعلی `iticket` | طبق قانون جدید، `cinema.iticket_attempts` یا استخراج اطلاعات native به جدول اختصاصی و نگه‌داشتن attempt عمومی بدون اطلاعات اختصاصی. تضمین «قبل از تماس بیرونی، ثبت تلاش» نباید حذف شود. |
| ۲ | `payment_gateway.sessions` | `PaymentGatewayDbContext.cs:22`؛ Provider در کنار token/ref/trace/PAN و RawCallback JSON | Session پرداخت مشترک باقی بماند؛ token، raw callback و جزئیات هر درگاه به `payment_gateway.<provider>_sessions`/`<provider>_callbacks` استخراج شود. اسامی prefix باید از enum/provider registry فعلی تثبیت شوند. Callback تکراری و ledger idempotency معیار پذیرش است. |

## جدول‌های مشترکی که داده اختصاصی در آن‌ها جاسازی شده

- `flights.flight_bookings`: `ConfigureProvider` و `ConfigureProviderBooking` در `FlightBookingConfiguration.cs` دارای `provider_snapshot` و snapshot رزرو خارجی هستند. اصل Booking مشترک است؛ payload به جدول اختصاصی با BookingId استخراج شود.
- `flights.flight_offer_snapshots`: fare snapshot متعلق به Booking شامل `ProviderFareId` و provider trace است؛ بخش fare مشترک و payload native باید هنگام اصلاح بالا جدا شوند.
- `commerce.order_items`: `ProviderPayloadJson` در مدل و `CommerceDbContext` نگهداری می‌شود. عنوان/قیمت/اقلام سفارش تاریخی مشترک بمانند؛ payload خارجی به جدول provider منتقل شود.
- `hotels.hotel_bookings` و `hotel_requests`: داشتن Provider/ProviderBookingCode و snapshot قیمت/هتل لزوماً نقض نام‌گذاری نیست؛ این‌ها داده تراکنش و شواهد تاریخی رفاهی‌اند. هر payload خام اختصاصی که بعداً به آن‌ها اضافه شود باید در جدول prefixدار باشد.
- `cinema.orders`، `charge.charge_requests`، `commerce.orders/checkout_sessions` و `payment_gateway.sessions`: جدول کسب‌وکار را صرف داشتن ProviderKey تکثیر نکنیم؛ فقط state/payload بیرونی را استخراج کنیم.

## مواردی که به‌اشتباه نباید تغییر نام داده شوند

- `flights.airports`، `search_cities` و `search_memberships`: کاتالوگ جغرافیا/عضویت جست‌وجو با IATA و کشور است و در نسخه فعلی import دستی curate شده دارد؛ فقط provider snapshot نیست. import در `FlightAirportDataSeeder.cs`، منابع و تصمیم تقدم JSON در `scripts/flights/search-catalog-20260906/source-audit.md` ثبت شده است. اگر در آینده کد مقصد اختصاصی پرووایدر وارد شد، mapping جدا با پیشوند پرووایدر لازم است.
- جدول‌های Orders/Wallets، کاربران، سازمان‌ها، فروشگاه‌های داخلی، و `store.product_sessions` داده کسب‌وکار خود رفاهی‌اند. `product_sessions` سانس محصول است و session پرووایدر نیست.
- providerهای موجود در Infrastructure که فقط HttpClient/Redis/memory cache دارند، جدول PostgreSQL اختصاصی ندارند تا نام آن نقض شده باشد. در checkout فعلی هیچ جدول فعال `lamaso_*` یا adapter لاماسو در مدل EF Hotels دیده نشد؛ اسکریپت V2 این جدول‌های نگاشت را می‌سازد ولی اتصال runtime را به‌تنهایی پیاده نمی‌کند.

## ترتیب پیشنهادی اصلاح runtime

۱. مدل کشور و قراردادهای References: Country، CountryCode استان، جست‌وجو/فیلتر کشور، LocationKind مقصد و resolve شناسه‌های بازنشسته از `city_merge_history`. Validator شهر باید یکتایی نام نرمال‌شده+استان را نیز بررسی کند؛ اکنون فقط Slug+ProvinceId یکتا است.

۲. مدل/Repository نگاشت Hotels: برای هر provider Entity و EF configuration در همان پنج لایه Hotels، با جدول‌های V2 اضافه شود. مصرف‌کننده مرجع فقط از References.Application.Contracts اطلاعات بگیرد. بقیه runtime هنوز این جدول‌های SQL را نمی‌خواند.

۳. داده هر جدول قدیمی را براساس provider واقعی inventory کنید؛ ردیف بدون discriminator قابل اعتماد را جدا کنید. payload اختصاصی از aggregate عمومی جدا و کلیدهای historical/trace/idempotency حفظ شود. migrations نام جدول و index/FK را با هم اصلاح کنند؛ صرف RenameTable برای جدول مختلط کافی نیست.

۴. برای انتشار، از مسیر expand → انتقال با کنترل شمار/هش → خواندن از ساختار جدید → توقف نوشتن قدیم → حذف ساختار قدیم پس از اطمینان استفاده شود. عملیات در حال اجرا و retryهای پس از restart باید از هر دو ساختار قابل بازیابی باشند. هیچ برداشت، رزرو، صدور تیکت یا refund برای انتقال داده دوباره اجرا نشود.

۵. آزمون‌های contract و idempotency، خرید/تأیید/کنسلی/recovery هر ماژول، build و migration consistency اجرا شوند. به آزمون مدل اضافه شود که Entityهای مشخص‌شده به‌عنوان provider-owned، در schema ماژول و با prefix مصوب map شده باشند؛ وجود یک property با نام Provider به‌تنهایی معیار آزمون نباشد.

این نوبت فقط SQL جغرافیا، قاعده مخزن و این گزارش تغییر کرده‌اند؛ rename گسترده جدول‌های runtime انجام نشده است.


## به‌روزرسانی چرخه رزرو — ۲۰۲۶-۱۰-۲۰

داده رزرو خارجی Hotels در Migration HotelsLamasoBookingLifecycle به hotels.lamaso_reservations منتقل و تلاش‌های خارجی در hotels.lamaso_operation_attempts ثبت شدند. تغییر نام فقط پس از کنترل تک‌پرووایدر بودن داده انجام می‌شود؛ شناسه و Snapshotها حفظ می‌شوند. جدول‌های provider_inventory، offer_snapshots و catalogue_city_references و payloadهای اختصاصی داخل aggregateهای مشترک همچنان برای تفکیک مستقل در دامنه گزارش باقی‌اند؛ در این تغییر به نام یک پرووایدر تغییر نکردند. Migrationهای تاریخی نام‌های قدیمی را حفظ می‌کنند.
