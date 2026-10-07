using System.Text;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Cinema.Domain;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Persistence;
using Refahi.Modules.Cinema.Infrastructure.Printing;
using Xunit;
namespace Refahi.Modules.Cinema.Tests;
public sealed class CinemaIntegrationTests
{
    [CinemaPostgresFact]
    public async Task Migration_unique_indexes_concurrency_and_cross_connection_lock_work_on_postgres()
    {
        var connection=Environment.GetEnvironmentVariable("CINEMA_TEST_POSTGRES")!;
        var options=new DbContextOptionsBuilder<CinemaDbContext>().UseNpgsql(connection).Options;
        await using var db=new CinemaDbContext(options);await db.Database.MigrateAsync();
        var user=Guid.NewGuid();var key=Guid.NewGuid().ToString("N");
        var first=CinemaOrder.Create(user,"iticket","schedule",key,"fingerprint","{}","[]","cinema");
        first.BeginAttempt("reserve").Complete();
        db.Orders.Add(first);await db.SaveChangesAsync();
        await using var another=new CinemaDbContext(options);
        another.Orders.Add(CinemaOrder.Create(user,"iticket","schedule",key,"fingerprint","{}","[]","cinema"));
        await Assert.ThrowsAsync<DbUpdateException>(()=>another.SaveChangesAsync());another.ChangeTracker.Clear();
        var stale=await another.Orders.Include(x=>x.Attempts).SingleAsync(x=>x.Id==first.Id);
        Assert.Equal("Completed",Assert.Single(stale.Attempts).Outcome);
        first.Touch();await db.SaveChangesAsync();stale.Touch();await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>another.SaveChangesAsync());
        var gate=new PostgresCinemaMutationLock(connection);var lease=await gate.AcquireAsync(first.Id,default);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{await using var other=await gate.AcquireAsync(first.Id,timeout.Token);});
        await lease.DisposeAsync();await using var available=await gate.AcquireAsync(first.Id,default);
    }
    [CinemaBrowserFact]
    public async Task Pdf_contains_a_real_document_and_rejects_unissued_ticket()
    {
        var renderer=new CinemaTicketRenderer();var map=new CinemaSeatMap("schedule",new("show","نمایش آزمایشی","theater",null,null,null,null,90,null,[]),new("place","سالن آزمایشی",null,null),"سالن یک",DateTimeOffset.UtcNow.AddDays(1),null,[new("seat","block","الف","۱۲",0,0,100000,"available",true,null)]);
        var ticket=new CinemaOrderView(Guid.NewGuid(),Guid.NewGuid(),"R-TEST",1,"Paid","Issued","None",null,100000,100000,0,0,0,map,["seat"],"VALID-TEST-CODE","SAMFA-TEST",false,true,true,null);
        var bytes=await renderer.RenderAsync(ticket,default);Assert.StartsWith("%PDF-",Encoding.ASCII.GetString(bytes[..5]));Assert.True(bytes.Length>5000);
        var output=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(output);await File.WriteAllBytesAsync(Path.Combine(output,"cinema-ticket.pdf"),bytes);
        await Assert.ThrowsAsync<CinemaException>(()=>renderer.RenderAsync(ticket with{CanDownload=false},default));
    }
}
public sealed class CinemaPostgresFactAttribute : FactAttribute
{
    public CinemaPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CINEMA_TEST_POSTGRES")))Skip="Set CINEMA_TEST_POSTGRES to an isolated test database.";}
}
public sealed class CinemaBrowserFactAttribute : FactAttribute
{
    public CinemaBrowserFactAttribute(){if(Environment.GetEnvironmentVariable("CINEMA_BROWSER_TESTS")!="1")Skip="Install Playwright Chromium and set CINEMA_BROWSER_TESTS=1.";}
}
