# گزارش رخداد تولید Charge در ۱۴۰۵/۰۴/۲۹

## دامنه بررسی

- لاگ تولید: `_prod_api_logs-2.txt`
- بکاپ PostgreSQL: `refahi-db_14050429-1`
- درخواست Charge: `238385b7-7a0b-4a67-abad-93f636de9cb1`
- سفارش: `ORD-260720-086A30` (`f0706cb0-a25d-4abd-b0e9-bf8d7e6964c9`)
- مبلغ: `50000 IRR`

شماره‌های همراه و payloadهای حساس در این گزارش تکرار نشده‌اند.

## خلاصه رخداد

سفارش با موفقیت از Wallet پرداخت و مبلغ آن Capture شده، اما خرید PinCharge در Eniac با HTTP 400 و کد 120 رد شده است. Traceهای بعدی چهار بار کد 117 (`اطلاعاتی برای نمایش وجود ندارد`) برگردانده‌اند. پردازشگر باید این نتیجه قطعی را ثبت و سفارش را از مسیر رسمی Orders/Wallet refund می‌کرد، اما ثبت `ChargeFulfillmentAttempt` جدید به اشتباه با SQL از نوع `UPDATE` انجام شده است. چون attempt هنوز در دیتابیس وجود نداشته، صفر ردیف تغییر کرده و EF Core خطای `DbUpdateConcurrencyException` داده است.

تراکنش `SaveChanges` در هر چرخه rollback شده، بنابراین:

- جدول `charge.charge_fulfillment_attempts` خالی مانده است.
- `ChargeRequest` در وضعیت `Processing` باقی مانده است.
- `ReconciliationCount` صفر مانده است.
- Worker پس از انقضای lease تقریباً هر پنج دقیقه Trace را تکرار کرده است.
- Provider call audit مستقل از aggregate ذخیره شده و چهار Trace ناموفق در بکاپ قابل مشاهده است.

## خطاهای مشاهده‌شده

### ۱. خطای کد: ثبت attempt با UPDATE

لاگ SQL قبل از exception نشان می‌دهد EF دستور زیر را اجرا کرده است:

```sql
UPDATE charge.charge_fulfillment_attempts
SET ...
WHERE id = @p13;
```

بلافاصله پس از آن `DbUpdateConcurrencyException` ثبت شده است. این exception تعارض واقعی دو نسخه از `charge_requests.xmin` نبود؛ فرمان صفر-ردیفی مربوط به attempt جدید علت خطا بوده است.

### ۲. خطای عملیاتی Eniac: کد 120

در خرید اولیه PinCharge، Eniac پاسخ HTTP 400 با پیام «شما امکان دسترسی به این سرویس را ندارید» و کد 120 داده است. این مورد به دسترسی credential/merchant به سرویس PinCharge مربوط است و با تغییر کد داخلی قابل اعطا نیست.

### ۳. نتیجه قطعی Trace: کد 117

چهار Trace در زمان‌های تقریبی ۱۲:۱۵، ۱۲:۲۰، ۱۲:۲۵ و ۱۲:۳۰ پاسخ کد 117 و داده null داده‌اند. منطق فعلی این کد را failure قطعی در نظر می‌گیرد و پس از رفع persistence باید refund رسمی را آغاز کند.

### ۴. سوابق قدیمی و نامرتبط PaymentGateway

بکاپ سه session قدیمی در تاریخ ۱۴۰۵/۰۳/۱۳ با خطاهای «شماره ترمینال الزامی است» و «آدرس سرور پذیرنده نامعتبر است» دارد. وجود sessionهای موفق جدیدتر نشان می‌دهد این‌ها رخداد جاری این لاگ نیستند و به اصلاح Charge ارتباط ندارند.

## وضعیت مالی در بکاپ

- PaymentIntent متناظر Capture شده است.
- Payment به مبلغ `50000 IRR` وجود دارد.
- Payment allocation و ledger debit متناظر وجود دارند.
- هیچ Refund متناظری در snapshot وجود ندارد.
- بنابراین SQL دستی روی Wallet، Ledger یا Balance مجاز نیست؛ بازگشت وجه فقط باید از `CancelOrderCommand` و جریان رسمی Refund انجام شود.

## اصلاح انجام‌شده

- متد `AddFulfillmentAttemptAsync` به repository مالک aggregate اضافه شد و پردازشگر attempt تازه را پیش از `SaveChanges` صریحاً با state `Added` ثبت می‌کند. مسیرهای Purchase، Trace و exception همگی از این مسیر مشترک استفاده می‌کنند.
- پاسخ HTTP ناموفق Eniac اکنون parse می‌شود تا `eniacResultCode` و پیام امن تامین‌کننده در audit و تصمیم پردازش ثبت شوند.
- خطاهای provider به دو گروه تفکیک شدند:
  - رد قطعی Purchase، مانند HTTP 400 / کد 120، بدون Trace بی‌فایده وارد مسیر Failed و Refund می‌شود.
  - timeout، transport، پاسخ خراب و شکست HTTP/احراز هویت هنگام Trace همچنان مبهم هستند و تا تعیین نتیجه وارد Reconciliation می‌شوند؛ در نتیجه خطای استعلام باعث Refund اشتباه نمی‌شود.
- یک `ChargeRefundProcessor` مشترک برای Worker و عملیات Admin اضافه شد. Refund قبل از فراخوانی Orders به‌صورت پایدار در وضعیت `Refunding` ذخیره می‌شود و کلید idempotency، دلیل، تعداد تلاش، آخرین خطا و زمان تلاش نگهداری می‌شوند.
- وضعیت `Refunding` وارد صف کار Worker شد. اگر سرویس قبل/بعد از `CancelOrderCommand` قطع شود، پس از پایان lease همان عملیات با همان idempotency key ادامه پیدا می‌کند. این موضوع از دوباره‌برگشت‌زدن Wallet جلوگیری می‌کند.
- در صورت شکست موقت Orders/Wallet، درخواست دیگر در وضعیت غیرقابل‌بازیابی رها نمی‌شود؛ خطا ثبت، lease آزاد و retry با backoff زمان‌بندی می‌شود.
- Refund از وضعیت `Fulfilled` و سایر وضعیت‌های نامعتبر در Domain رد می‌شود.
- migration `20260720104534_ChargeAddRefundRecovery` برای ستون‌های بازیابی Refund اضافه شد و باید پیش از اجرای نسخه جدید اعمال شود.

## اعتبارسنجی

- `dotnet test tests/Refahi.Modules.Charge.Tests/Refahi.Modules.Charge.Tests.csproj --no-restore`
  - 25 Passed، 0 Failed
- `dotnet build Refahi.Backend.slnx --no-restore`
  - 0 Error، 8 Warning موجود از قبل
- `git diff --check`
  - بدون خطای whitespace

تست‌ها علاوه بر جریان‌های قبلی، ثبت صحیح attempt، قطعی بودن Purchase HTTP 400، مبهم ماندن خطای Trace، حفظ idempotency key در retry، بازیابی Refund ناموفق و ممنوع بودن Refund درخواست Fulfilled را پوشش می‌دهند.

## اقدام عملیاتی پس از استقرار

1. دسترسی merchant/credential فعلی Eniac به `/api/Merchant/BuyPinCharge` و category `1005` توسط Eniac تأیید شود.
2. migration `20260720104534_ChargeAddRefundRecovery` اعمال و سپس نسخه اصلاح‌شده deploy شود؛ heartbeat Worker نیز کنترل شود.
3. پس از پایان lease، Worker می‌تواند همان درخواست را Trace کند؛ در صورت تکرار کد 117، باید attempt ثبت و refund رسمی اجرا شود. هر وضعیت `Refunding` باقی‌مانده نیز توسط Worker قابل ادامه است.
4. برای کنترل‌شده‌تر بودن عملیات می‌توان Worker Charge را موقتاً متوقف و endpoint رسمی `trace-again` را طبق `charge-production-recovery-runbook.md` اجرا کرد.
5. پس از پردازش، موارد زیر کنترل شوند:
   - حداقل یک `charge_fulfillment_attempts` برای request وجود داشته باشد.
   - ChargeRequest به `Refunded` یا در صورت خطای جبرانی به `ManualReview` رسیده باشد.
   - Refund و ledger credit دقیقاً `50000 IRR` و روی allocation اصلی باشند.
   - Order دیگر در وضعیت Paid بدون تعیین تکلیف Charge باقی نماند.
6. هیچ Purchase مجدد، SQL دستی مالی یا تغییر مستقیم status انجام نشود.

## ریسک‌های خارج از دامنه

build کل solution هشدار `NU1903` برای آسیب‌پذیری high-severity در `Microsoft.OpenApi 2.3.0` و warningهای قدیمی nullable را نشان می‌دهد. این موارد عامل رخداد Charge نیستند و باید در یک تغییر مستقل بررسی شوند.
