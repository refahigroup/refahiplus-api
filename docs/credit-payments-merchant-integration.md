# راهنمای اتصال پذیرنده به درگاه پرداخت اعتباری

این API برای سرور پذیرنده خارجی است. `clientId` و `secret` فقط در سرور نگهداری شوند. مرورگر مشتری فقط به `checkoutUrl` هدایت می‌شود؛ بازگشت مرورگر از صفحه پرداخت اثبات موفقیت نیست.

## آماده‌سازی پذیرنده

اپراتور از مسیرهای Admin، پذیرنده مستقل، کیف درآمد از نوع Merchant، پایانه فعال، دسته و حدود مبلغ، URLهای بازگشت مجاز، اعتبارنامه با scope لازم و webhook HTTPS را ثبت می‌کند. سیاست مالی پایانه باید به کیف درآمد همان پذیرنده اشاره کند. راز اعتبارنامه و راز webhook هنگام صدور فقط یک‌بار نمایش داده می‌شوند؛ آن‌ها را در secret store نگه دارید.

همه مسیرهای زیر پیشوند `/api/credit-payments/v1` دارند. پاسخ‌ها در پوشش `ApiResponse<T>` برمی‌گردند؛ مقدار اصلی در `data` است. خطای برنامه در `errors.code[0]` قرار می‌گیرد. درخواست‌های پذیرنده از HTTP Basic با `clientId:secret` روی TLS استفاده می‌کنند. هر اعتبارنامه فقط به پذیرنده، پایانه و scope خودش دسترسی دارد. از ارسال اعتبارنامه به کد مرورگر خودداری کنید.

## چرخه پرداخت

1. سرور پذیرنده با `POST /payments` و هدر `Idempotency-Key` یک پرداخت می‌سازد. بدنه: `merchantOrderId`، `amountMinor`، `currency` برابر `IRR`، `returnUrl` ثبت‌شده، و در صورت نیاز `description`، `expiresInSeconds` و `metadata`. پاسخ `201` شامل `paymentId`، `checkoutUrl` و `expiresAt` است. برای retry همان سفارش، همان کلید و همان بدنه را بفرستید. کلید جدید ممکن است پرداخت جدید ایجاد کند.
2. مرورگر مشتری را به `checkoutUrl` هدایت کنید. صفحه Hosted Checkout احراز هویت، تأمین اعتبار، تأیید و نمایش وضعیت را انجام می‌دهد. اطلاعات کیف، تخصیص و مقصد مالی را در درخواست پرداخت مشتری قرار ندهید.
3. پس از بازگشت مشتری، **سرور** با `GET /payments/{paymentId}` وضعیت را استعلام کند. فقط `status=succeeded` همراه با شناسه و مبلغ/واحد پول مورد انتظار را موفق تلقی کنید. `processing` یا پاسخ نامعلوم باید دوباره استعلام شود؛ آن را شکست قطعی ندانید. `capturedAmountMinor` و `refundedAmountMinor` برای حسابرسی در پاسخ inquiry موجودند.
4. webhook راه دوم اطلاع‌رسانی است. پس از دریافت معتبر آن نیز وضعیت را با همان `paymentId` و قواعد بالا ثبت کنید. پردازش رویداد بر اساس `eventId` باید idempotent باشد؛ ارسال مجدد همان رویداد مجاز است.

## اعتبارسنجی webhook

بدنه خام HTTP را بدون بازنویسی JSON نگه دارید. هدرهای `X-Refahi-Webhook-Event-Id`، `X-Refahi-Webhook-Timestamp`، `X-Refahi-Webhook-Signature`، `X-Refahi-Webhook-Event-Type` و `X-Refahi-Webhook-Version` ارسال می‌شوند. امضای نسخه ۱ به شکل `v1=` به‌علاوه hex کوچکِ `HMAC-SHA256(secret, UTF8(eventId + "." + unixTimestamp + ".") || rawBody)` است. امضا را با مقایسه زمان‌ثابت و timestamp را با پنجره زمانی کوتاه بررسی کنید؛ `eventId` را برای جلوگیری از اجرای تکراری ثبت کنید. سپس پاسخ 2xx بدهید. پاسخ غیر 2xx موجب تلاش مجدد و در نهایت ورود به صف خطا می‌شود. رویدادهای پرداخت و استرداد، نتیجه مالی را از projection ثبت‌شده تولید می‌کنند.

## لغو و استرداد

`POST /payments/{paymentId}/cancel` با `Idempotency-Key` و `reason`، ممکن است `200` یا `202 processing` بدهد. در حالت processing نتیجه را با inquiry پیگیری کنید. `POST /payments/{paymentId}/refunds` با `Idempotency-Key`، `amountMinor`، `merchantRefundReference` و `reason` پاسخ `202` می‌دهد؛ این پاسخ فقط پذیرش درخواست است. نتیجه را با `GET /refunds/{refundId}` یا `GET /payments/{paymentId}/refunds` دنبال کنید. پس از timeout، همان کلید idempotency را تکرار و سپس inquiry کنید. استرداد موفق را از redirect یا پذیرش اولیه نتیجه نگیرید.

## خطا و بازیابی

برای timeout و 5xx نتیجه عملیات را نامعلوم فرض کنید و با کلید idempotency ثابت retry یا inquiry انجام دهید. 401/403 نیازمند بررسی اعتبارنامه و scope است؛ 409 یعنی درخواست یا وضعیت فعلی تعارض دارد و باید inquiry شود؛ 429 باید با فاصله مناسب retry شود. `paymentId`، `merchantOrderId` و کلید idempotency را در گزارش پذیرنده نگه دارید، اما secret، URL دارای token، OTP و کوکی را log نکنید.
