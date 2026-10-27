using System.Xml.Linq;
using Xunit;
namespace Refahi.Modules.Catalogue.Tests;
public class CatalogueArchitectureTests
{
 [Fact]public void CatalogueDoesNotDependOnHotelsAndHotelsUsesOnlyCatalogueContracts(){
 var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Refahi.Backend.slnx")))root=root.Parent;
 Assert.NotNull(root);var source=Path.Combine(root!.FullName,"src");
 foreach(var dir in Directory.GetDirectories(source,"Refahi.Modules.Catalogue.*"))foreach(var file in Directory.GetFiles(dir,"*.csproj"))Assert.DoesNotContain(References(file),x=>x.Contains("Refahi.Modules.Hotels."));
 foreach(var dir in Directory.GetDirectories(source,"Refahi.Modules.Hotels.*"))foreach(var file in Directory.GetFiles(dir,"*.csproj"))Assert.All(References(file).Where(x=>x.Contains("Refahi.Modules.Catalogue.")),x=>Assert.Contains("Refahi.Modules.Catalogue.Application.Contracts",x));
 }
 static IEnumerable<string> References(string file)=>XDocument.Load(file).Descendants("ProjectReference").Select(x=>x.Attribute("Include")!.Value);
}
