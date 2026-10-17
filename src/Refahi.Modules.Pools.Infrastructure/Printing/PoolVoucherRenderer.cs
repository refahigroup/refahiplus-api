using Refahi.Modules.Pools.Application.Contracts;
using System.Net;
using Microsoft.Playwright;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Infrastructure.Printing;
public sealed class PoolVoucherRenderer : IPoolVoucherRenderer
{
    private static readonly SemaphoreSlim Capacity = new(2);
    public async Task<byte[]> RenderAsync(PoolPurchaseDto purchase, CancellationToken ct)
    {
        await Capacity.WaitAsync(ct);
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync(); await page.RouteAsync("**/*", r => r.AbortAsync());
            await using var font = typeof(PoolVoucherRenderer).Assembly.GetManifestResourceStream("PoolVoucherFont.woff2")!;
            using var buffer = new MemoryStream(); await font.CopyToAsync(buffer, ct);
            static string H(string? s) => WebUtility.HtmlEncode(s) ?? "";
            static string Status(string? value) => value switch { "Issued"=>"صادر شده", "Redeemed"=>"مصرف شده", "Revoked"=>"باطل شده", "Expired"=>"منقضی شده", _=>"در حال صدور" };
            var cards = string.Join("", purchase.Requests.Select(r => $"<section><h1>ووچر رفاهی‌پلاس</h1><h2>{H(r.PoolName)}</h2><p>{H(r.Date)} · {H(r.Time)}</p><p>بزرگسال: {r.AdultCount} · کودک: {r.ChildCount}</p><p>مبلغ: {r.TotalMinor / 10m:N0} تومان</p><p>شماره سفارش: {H(purchase.OrderNumber)}</p><p>کد ووچر: <bdi>{H(r.Code)}</bdi></p><p>وضعیت: {H(Status(r.Status))}</p><p>پذیرش کد فقط از طریق سامانه رفاهی و در بازه سانس انجام می‌شود. ظرفیت خارجی استخر رزرو نشده است.</p></section>"));
            var html = $"<!doctype html><html lang='fa' dir='rtl'><meta charset='utf-8'><style>@font-face{{font-family:voucher;src:url(data:font/woff2;base64,{Convert.ToBase64String(buffer.ToArray())})}}body{{font-family:voucher;padding:24px}}section{{break-after:page;border:1px solid #ccc;padding:24px}}section:last-child{{break-after:auto}}bdi{{font-family:monospace;overflow-wrap:anywhere}}</style>{cards}</html>";
            await page.SetContentAsync(html, new() { Timeout = 15000 }); await page.EvaluateAsync("document.fonts.ready");
            ct.ThrowIfCancellationRequested(); return await page.PdfAsync(new() { Format = "A4", PrintBackground = true });
        }
        catch (PlaywrightException) { throw new PoolException("سامانه چاپ ووچر موقتاً در دسترس نیست",503,"PDF_UNAVAILABLE"); }
        finally { Capacity.Release(); }
    }
}
