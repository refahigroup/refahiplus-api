# Runbook ترمیم سفارش‌های Charge

این Runbook فقط پس از انتشار migrationهای `ChargeAddProviderCallAudit` و `OrdersAddPayableUntil` اجرا می‌شود. هیچ Purchase مجددی برای سفارش پرداخت‌شده مجاز نیست و هیچ تغییر مالی با SQL دستی انجام نمی‌شود.

## پیش‌نیاز و توقف امن

1. backup قابل‌بازیابی PostgreSQL تهیه و restore آزمایشی آن تأیید شود.
2. فقط Workerهای Charge در پنجره‌ی نگهداری متوقف شوند؛ API و Wallet worker برای اجرای commandهای رسمی فعال بمانند.
3. برای هر هشت Order، snapshot تازه‌ی Order، ChargeRequest، PaymentIntent، allocation، Payment، Refund، Ledger و WalletBalance با timestamp و شناسه اپراتور در تیکت عملیات ثبت شود.
4. اگر داده‌ی زنده با گزارش مبنا متفاوت است، ادامه‌ی Runbook متوقف و تحلیل جدید انجام شود.
5. credential فعلی Eniac از secret store rotate شود؛ مقدار credential در log، تیکت یا command ثبت نشود.

## سه سفارش پرداخت‌شده

سفارش‌ها: `ORD-260708-887089`، `ORD-260709-FD804E` و `ORD-260716-65BA26`.

1. برای ChargeRequest متناظر، فقط `POST /api/charge/admin/charge-requests/{id}/trace-again` اجرا شود. correlation id و پاسخ پالایش‌شده از `GET /api/charge/admin/charge-requests/{id}/provider-calls` به تیکت پیوست شود.
2. اگر Trace/Report موفقیت قطعی و RRN/Trace معتبر داد، `POST .../{id}/confirm-fulfilled` با evidence مرجع اجرا شود.
3. اگر شکست قطعی یا NotFound قطعی بود، `POST .../{id}/refund` با reason و idempotency key ثابت به فرم `charge-recovery-refund-{chargeRequestId}` اجرا شود.
4. نتیجه‌ی مبهم در ManualReview باقی بماند و گزارش رسمی Eniac و گردش مالی به audit پیوست شود.
5. جمع Payment، Refund و Ledger این سه سفارش باید با مبلغ مبنای `450000 IRR` تطبیق داده شود؛ مغایرت مانع خروج از پنجره‌ی نگهداری است.

## پنج سفارش پرداخت‌نشده

- `ORD-260708-50410C` و `ORD-260708-81AC75`: از command رسمی CancelOrder با idempotency key ثابت استفاده شود؛ lifecycle باید ChargeRequest را Expired کند.
- `ORD-260708-70A4B1`: پس از تأیید نبود allocation و Hold، CancelOrder اجرا و وضعیت Intent/Order/ChargeRequest دوباره کنترل شود.
- `ORD-260708-96F5ED`: ابتدا endpoint محافظت‌شده‌ی `POST /api/wallets/admin/payment-intents/{intentId}/repair-orphan-hold` با `dryRun=true`، `expectedOrderId` و idempotency key ثابت اجرا شود. فقط اگر نتیجه `Repairable` و مقادیر before مطابق snapshot بود همان request با `dryRun=false` تکرار شود. سپس Order لغو شود.
- `ORD-260708-25B7EC`: بعد از صفرشدن drift مورد قبلی از مسیر رسمی CancelOrder/Release آزاد شود.

repair endpoint با قفل تراکنشی Intent و Wallet، guard الگوی Hold یتیم و compensating `ReleaseHold` کار می‌کند؛ در صورت هر تفاوتی بدون تغییر داده متوقف می‌شود.

## کنترل نهایی و بازگشت Worker

1. هر ChargeRequest پرداخت‌شده حداقل یک FulfillmentAttempt داشته باشد.
2. پنج سفارش پرداخت‌نشده Cancelled/Expired و بدون pending غیرمجاز باشند.
3. Wallet مورد ترمیم نسبت به snapshot قبل دقیقاً `50000 IRR` available بیشتر و `50000 IRR` pending کمتر داشته باشد؛ موجودی مطلق hardcode نشود.
4. audit روزانه Wallet باید صفر برای allocation mismatch، orphan hold، captured-without-payment، released-without-ledger و projection drift گزارش کند.
5. health endpoint `GET /api/health/eniac` سالم باشد. metricهای `charge.provider.failures`، `charge.worker.heartbeats` و `charge.reconciliation.batch_size` در telemetry دیده شوند.
6. alert برای نبود heartbeat بیش از دو دقیقه، افزایش provider failure، ManualReview باز، reconciliation عقب‌افتاده، Intent رزروشده بیش از ۳۰ دقیقه و هر integrity drift فعال شود.
7. Workerهای Charge فعال شوند و backlog تا صفر مانیتور شود. اجرای دوباره‌ی commandها باید همان نتیجه‌ی idempotent قبلی را برگرداند.

## Rollback عملیاتی

در صورت خطا Worker متوقف می‌ماند، command جدید اجرا نمی‌شود و snapshot پس از خطا ثبت می‌گردد. migrationها در حضور داده‌ی audit rollback نمی‌شوند؛ بازگشت مالی فقط با compensating command رسمی و تأیید مسئول عملیات انجام می‌شود.
