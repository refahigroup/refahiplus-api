using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Infrastructure.Printing;
using Refahi.Modules.Pools.Infrastructure.Security;
using Xunit;
namespace Refahi.Modules.Pools.Tests;
public sealed class PoolVoucherPrintingTests
{
 [Fact]public async Task Stored_codes_render_as_one_pdf_page_per_request_without_issuing_new_codes()
 {
  var now=DateTimeOffset.UtcNow;var v=new PoolVoucherDto(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"استخر", "1405/07/22","10:00 - 12:00",2,1,1000000,now,now.AddHours(2),"ExternalReservationBypassed","Issued","0123456789ABCDEF0123456789ABCDEF",null);
  var p=new PoolPurchaseDto(Guid.NewGuid(),Guid.NewGuid(),"R-1",1,2000000,"Paid",now,false,true,[v,v with{RequestId=Guid.NewGuid(),Code="FEDCBA9876543210FEDCBA9876543210"}]);
  var renderer=new PoolVoucherRenderer();var pdf=await renderer.RenderAsync(p,default);var again=await renderer.RenderAsync(p,default);
  Assert.StartsWith("%PDF-",Encoding.ASCII.GetString(pdf));Assert.Equal(2,Regex.Matches(Encoding.Latin1.GetString(pdf),@"/Type /Page\b").Count);
  Assert.Equal(2,Regex.Matches(Encoding.Latin1.GetString(again),@"/Type /Page\b").Count);Assert.Equal(v.Code,p.Requests[0].Code);
 }
 [Fact]public void Codes_are_recoverable_only_with_the_correct_data_protection_purpose()
 {
  var provider=new EphemeralDataProtectionProvider();var protector=new PoolCodeProtector(provider);var cipher=protector.Protect("test-voucher-code");Assert.DoesNotContain("test-voucher-code",cipher);Assert.Equal("test-voucher-code",protector.Unprotect(cipher));Assert.Throws<System.Security.Cryptography.CryptographicException>(()=>provider.CreateProtector("other-purpose").Unprotect(cipher));
 }
}
