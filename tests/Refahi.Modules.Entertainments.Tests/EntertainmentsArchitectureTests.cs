using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Xunit;
namespace Refahi.Modules.Entertainments.Tests;

public sealed class EntertainmentsArchitectureTests
{
    [Fact]
    public void Module_has_no_commerce_dependencies_and_provider_tables_have_own_schema()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Refahi.Backend.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var modules = Directory.GetDirectories(Path.Combine(root!.FullName, "src"), "Refahi.Modules.Entertainments.*");
        Assert.Equal(5, modules.Length);
        foreach (var module in modules)
        foreach (var project in Directory.GetFiles(module, "*.csproj"))
            Assert.DoesNotContain("Refahi.Modules.Commerce", File.ReadAllText(project), StringComparison.OrdinalIgnoreCase);
        using var db = new EntertainmentDbContext(new DbContextOptionsBuilder<EntertainmentDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.All(db.Model.GetEntityTypes(), x => Assert.Equal("entertainments", x.GetSchema()));
        Assert.Contains(db.Model.GetEntityTypes(), x => x.GetTableName() == "touristpanel_programs");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.GetTableName()!.StartsWith("provider_"));
        var commerce = Path.Combine(root.FullName, "src", "Refahi.Modules.Commerce.Infrastructure", "DI.cs");
        Assert.DoesNotContain("AddTouristPanelProvider", File.ReadAllText(commerce));
    }
}
