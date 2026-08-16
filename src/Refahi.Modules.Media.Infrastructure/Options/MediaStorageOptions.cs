namespace Refahi.Modules.Media.Infrastructure.Options;

public class MediaStorageOptions
{
    public const string Section = "MediaStorage";

    /// <summary>مسیر فیزیکی روی دیسک (متفاوت در Windows/Linux)</summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>آدرس عمومی مطلق برای بارگذاری فایل‌های رسانه</summary>
    public string LoadBaseUrl { get; set; } = string.Empty;

    public long MaxImageSizeBytes { get; set; } = 10L * 1024 * 1024;
    public long MaxVideoSizeBytes { get; set; } = 200L * 1024 * 1024;
}
