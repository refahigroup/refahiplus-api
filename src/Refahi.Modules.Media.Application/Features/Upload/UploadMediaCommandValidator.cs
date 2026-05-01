using FluentValidation;
using Refahi.Modules.Media.Application.Contracts.Commands;

namespace Refahi.Modules.Media.Application.Features.Upload;

public class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    public static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"];

    public static readonly string[] AllowedVideoTypes =
        ["video/mp4", "video/webm"];

    public const long MaxImageSize = 10L * 1024 * 1024;        // 10 MB
    public const long MaxVideoSize = 200L * 1024 * 1024;       // 200 MB

    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .NotEmpty().WithMessage("نام فایل الزامی است")
            .MaximumLength(500).WithMessage("نام فایل بسیار طولانی است");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("نوع محتوا الزامی است")
            .Must(ct =>
                AllowedImageTypes.Contains(ct?.ToLowerInvariant()) ||
                AllowedVideoTypes.Contains(ct?.ToLowerInvariant()))
            .WithMessage("نوع فایل پشتیبانی نمی‌شود");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("حجم فایل نامعتبر است");

        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var ct = cmd.ContentType?.ToLowerInvariant() ?? string.Empty;
            var isVideo = ct.StartsWith("video/", StringComparison.Ordinal);
            var max = isVideo ? MaxVideoSize : MaxImageSize;
            if (cmd.FileSizeBytes > max)
            {
                ctx.AddFailure(nameof(cmd.FileSizeBytes),
                    isVideo
                        ? "حجم ویدیو از حد مجاز بیشتر است (حداکثر ۲۰۰ مگابایت)"
                        : "حجم تصویر از حد مجاز بیشتر است (حداکثر ۱۰ مگابایت)");
            }
        });

        RuleFor(x => x.UploadedByUserId)
            .NotEmpty().WithMessage("شناسه کاربر آپلودکننده الزامی است");
    }
}
