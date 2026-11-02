using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public class DisposableDatabaseGuardTests
{
    [Theory]
    [InlineData("Host=production;Database=hotel_city_test_qa", "hotel_city_test", null)]
    [InlineData("Host=127.0.0.1;Database=application", "hotel_city_test", null)]
    [InlineData("Host=127.0.0.1;Database=hotel_city_testproduction", "hotel_city_test", null)]
    [InlineData("Host=127.0.0.1;Port=5432;Database=entertainments_test_qa", "entertainments_test", 55442)]
    public Task RejectsNonDisposableTargetsBeforeOpeningAConnection(string connection, string prefix, int? port) =>
        Assert.ThrowsAsync<InvalidOperationException>(() => Refahi.Tests.DisposablePostgres.ValidateAndProbeAsync(connection, prefix, port));
}
