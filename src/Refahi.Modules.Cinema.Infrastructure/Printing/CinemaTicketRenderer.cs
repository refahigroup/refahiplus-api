using System.Net;
using System.Globalization;
using Microsoft.Playwright;
using Refahi.Modules.Cinema.Application.Contracts;
namespace Refahi.Modules.Cinema.Infrastructure.Printing;
public sealed class CinemaTicketRenderer : ICinemaTicketRenderer
{
    private static readonly SemaphoreSlim Capacity = new(2);
    public async Task<byte[]> RenderAsync(CinemaOrderView ticket,CancellationToken ct)
    {
        if(!ticket.CanDownload)throw new CinemaException("بلیط هنوز صادر نشده یا لغو شده است");
        await Capacity.WaitAsync(ct);
        try
        {
            using var playwright=await Playwright.CreateAsync();
            await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
            await using var context=await browser.NewContextAsync();
            var page=await context.NewPageAsync();
            await page.RouteAsync("**/*",route=>route.AbortAsync());
            static string H(string? x)=>WebUtility.HtmlEncode(x)??"";
            var seats=ticket.Snapshot.Seats.Where(s=>ticket.SeatIds.Contains(s.Id));
            var assembly=typeof(CinemaTicketRenderer).Assembly;
            await using var font=assembly.GetManifestResourceStream("CinemaTicketFont.woff2")!;
            using var buffer=new MemoryStream();await font.CopyToAsync(buffer,ct);
            var iran=TimeZoneInfo.ConvertTime(ticket.Snapshot.StartsAt,TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"));
            var persian=new PersianCalendar();var day=$"{persian.GetYear(iran.DateTime)}/{persian.GetMonth(iran.DateTime):00}/{persian.GetDayOfMonth(iran.DateTime):00}";
            var encodedSeats=string.Join(" — ",seats.Select(s=>$"ردیف {H(s.Row)}، صندلی {H(s.Number)}"));
            var fontData=Convert.ToBase64String(buffer.ToArray());
            var html=$$"""
                <!doctype html><html lang="fa" dir="rtl"><meta charset="utf-8"><style>
                @font-face{font-family:ticket;src:url(data:font/woff2;base64,{{fontData}})}
                body{font-family:ticket,sans-serif;padding:32px;color:#18252b} h1{font-size:24px}
                .box{border:1px solid #ddd;border-radius:12px;padding:20px;margin:16px 0}
                </style><h1>بلیط رفاهی‌پلاس</h1><div class="box"><h2>{{H(ticket.Snapshot.Show.Title)}}</h2>
                <p>{{H(ticket.Snapshot.Place.Title)}} — {{H(ticket.Snapshot.Hall)}}</p><p>{{H(day)}} ساعت {{iran:HH:mm}}</p>
                <p>{{encodedSeats}}</p></div><p>شماره سفارش: {{H(ticket.OrderNumber)}}</p><p>کد بلیط: {{H(ticket.TicketCode)}}</p>
                <p>کد سمفا: {{H(ticket.SamfaCode)}}</p><p>مبلغ: {{ticket.TotalMinor/10:N0}} تومان</p></html>
                """;
            await page.SetContentAsync(html,new(){Timeout=15000});
            await page.EvaluateAsync("document.fonts.ready");ct.ThrowIfCancellationRequested();
            return await page.PdfAsync(new(){Format="A4",PrintBackground=true});
        }
        finally{Capacity.Release();}
    }
}
