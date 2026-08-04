using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Modules.Wallets.Application.Features.CreateWallet;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class CreateWalletCommandValidatorTests
{
    private readonly CreateWalletCommandValidator _validator = new();

    [Theory]
    [InlineData(WalletTypeCodes.Refahi)]
    [InlineData(WalletTypeCodes.Provider)]
    [InlineData("provider")]
    public void Accepts_supported_internal_wallet_types(string walletType)
    {
        var result = _validator.Validate(new CreateWalletCommand(Guid.NewGuid(), walletType, "IRR"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_unknown_wallet_type()
    {
        var result = _validator.Validate(new CreateWalletCommand(Guid.NewGuid(), "SYSTEM", "IRR"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateWalletCommand.WalletType));
    }
}
