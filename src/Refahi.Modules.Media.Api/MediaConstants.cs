namespace Refahi.Modules.Media.Api;

public static class MediaConstants
{
    /// <summary>سقف یک درخواست single (تصویر یا ویدیو) — ۲۵۰ مگابایت با حاشیه امن</summary>
    public const long MaxRequestBodyBytes = 250L * 1024 * 1024;

    /// <summary>سقف یک درخواست batch (حداکثر ۲۰ فایل) — ۱ گیگابایت</summary>
    public const long MaxBatchBodyBytes = 1024L * 1024 * 1024;

    /// <summary>حداکثر تعداد فایل در یک batch upload</summary>
    public const int MaxBatchFileCount = 20;
}
