# گیت انتشار و بازیابی درگاه پرداخت اعتباری

## قبل از فعال‌سازی

- نسخه‌های دقیق API، CPG و webapp و مهاجرت‌های CreditPayments/Wallets را ثبت کنید. از داده و key ring محافظت‌شده پشتیبان بگیرید؛ نسخه قبلی image و روش بازگردانی traffic آماده باشد.
- برای هر پذیرنده و پایانه فعال، کیف درآمد Merchant، دسته، حدود مبلغ، URL بازگشت، اعتبارنامه و webhook HTTPS را بازبینی کنید. هر سیاست پرداخت باید یکتا و مقصدش همان کیف درآمد پذیرنده باشد. `FinanceApprovalReference` فقط با شناسه تأیید واقعی مالی تنظیم شود.
- `CreditPayments:ProductionReadiness:SettlementLaunchMode=internal-wallet-transfer` و `CreditPayments:SettlementIntegration:PublicationEnabled=false` بماند. در این مدل تسویه خارجی در رفاهی انجام نمی‌شود. نتیجه به پذیرنده از inquiry و webhook داده می‌شود.
- رکوردهای integration event فعلی به‌عنوان سابقه داخلیِ تغییر مالی ساخته می‌شوند، اما در این مدل ارسال نمی‌شوند. شمارنده `UnpublishedIntegrationEvents` در خلاصه Admin به همین دلیل می‌تواند افزایش یابد؛ آن را با صف webhook اشتباه نگیرید و حجم این جدول را در پایش ظرفیت لحاظ کنید.
- کلید token و Data Protection از secret store و key ring مشترک/پایدار بارگذاری شوند. Hosted Checkout روی HTTPS با proxy هم‌مبدأ `/api/credit-payments/`، originهای صریح، و بدون cache برای HTML و API ارائه شود. `HostedCheckoutDeploymentEvidence` فقط پس از ثبت شاهد واقعی پر شود.
- `CreditPayments:Reconciliation:Enabled=true` و webhook فعال برای همه پایانه‌های منتشرشده لازم است. پاسخ Admin `GET /api/credit-payments/v1/admin/production-readiness` باید `ready` باشد. سطرهای `database`، `data-protection`، `merchant-policy-coverage`، `webhook-coverage`، `finance-approval`، `hosted-checkout`، `reconciliation` و `integration-event-transport` را جداگانه بررسی کنید. `/api/health/live` فقط زنده‌بودن فرایند را می‌سنجد.
- ۱۱ تست migration مرحله‌ای با مدل قدیمی Supplier عمداً `Skip` هستند؛ آزمون‌های migration مدل مستقل Merchant باید روی نسخه نهایی پاس شوند. این skipها به‌تنهایی مجوز ردکردن بررسی مسیر ارتقای داده واقعی نیستند.

## پایش و رسیدگی

- خلاصه عملیاتی Admin، صف بازیابی و موارد reconciliation را برای پرداخت/شارژ/لغو/استرداد معلق و webhookهای dead letter پایش کنید. هشدار 5xx، OTP و 429 غیرعادی، کهنگی صف، و مغایرت مالی داشته باشید. logها نباید checkout token، OTP، راز کوکی، Basic credential یا راز webhook را داشته باشند.
- در نتیجه نامعلوم، ابتدا inquiry پرداخت و وضعیت Wallet/Orders را با شناسه پرداخت بررسی کنید. تا روشن‌شدن نتیجه، عملیات مالی تازه با کلید جدید نسازید. سپس از endpoint بازیابی Admin برای همان منبع استفاده و دوباره inquiry کنید.
- webhook تحویل‌نشده را از صف delivery و کد شکست بررسی کنید؛ پس از رفع مقصد، resend Admin همان رویداد را انجام دهید. پاسخ موفق webhook جای inquiry پذیرنده را نمی‌گیرد.
- مغایرت مالی بحرانی، مقصد سیاست اشتباه یا پرداخت تکراری، توقف ایجاد پرداخت جدید برای پایانه درگیر و بررسی دستی با تیم مالی را لازم دارد. وضعیت را با تغییر مستقیم رکوردها «اصلاح» نکنید.

## بازگشت نسخه

ایجاد پرداخت جدید را برای پایانه‌های درگیر متوقف کنید، اما workerهای بازیابی را بدون بررسی وضعیت عملیات معلق خاموش نکنید. سازگاری schema و نسخه قبلی API را پیش از بازگردانی image تأیید کنید. پس از برگشت traffic، inquiry پرداخت‌های معلق، صف webhook و reconciliation را دوباره بررسی کنید. بازگردانی پایگاه داده فقط با تصمیم مالی/DBA و پس از تعیین تکلیف پرداخت‌های ثبت‌شده مجاز است.

## چک‌لیست آزمون production برای مالک محیط

TLS/proxy و هدرها؛ کوکی و CORS در مرورگر؛ health/readiness؛ OTP و احراز مجدد پس از حذف کوکی؛ اعتبار کافی/ناکافی؛ شارژ موفق/ناموفق/نامعلوم؛ refresh و confirm/cancel تکراری؛ inquiry و webhook امضاشده؛ استرداد؛ مانده مبدأ و مقصد؛ صف بازیابی و هشدار؛ نبود داده حساس در log/cache؛ و تمرین rollback. این آزمون‌ها فقط در محیط production توسط مالک محیط اجرا و ثبت شوند.
