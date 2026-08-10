using System.Reflection;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public class WalletAtomicWriterTests
{
    [Fact]
    public void IdempotencyRow_HasPublicParameterlessConstructorForDapper()
    {
        var rowType = typeof(WalletAtomicWriter).GetNestedType(
            "IdempotencyRow",
            BindingFlags.NonPublic
        );

        Assert.NotNull(rowType);
        Assert.NotNull(rowType.GetConstructor(Type.EmptyTypes));
    }
}
