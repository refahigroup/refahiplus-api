using Refahi.Modules.Identity.Application.Features.Addresses.AddAddress;
using Refahi.Modules.Identity.Domain.Entities;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class IdentityAddressValidationTests
{
    [Fact]
    public async Task Add_validator_accepts_optional_title_with_complete_address()
    {
        var validator = new AddAddressCommandValidator();

        var result = await validator.ValidateAsync(ValidCommand() with { Title = string.Empty });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Add_validator_rejects_non_ascii_or_non_ten_digit_postal_code()
    {
        var validator = new AddAddressCommandValidator();

        var persianDigits = await validator.ValidateAsync(ValidCommand() with { PostalCode = "۱۲۳۴۵۶۷۸۹۰" });
        var letters = await validator.ValidateAsync(ValidCommand() with { PostalCode = "12345A7890" });

        Assert.Contains(persianDigits.Errors, error => error.PropertyName == "PostalCode");
        Assert.Contains(letters.Errors, error => error.PropertyName == "PostalCode");
    }

    [Theory]
    [InlineData(null, "2")]
    [InlineData("12", null)]
    [InlineData("", "2")]
    public async Task Add_validator_requires_plate_and_unit(string? plate, string? unit)
    {
        var validator = new AddAddressCommandValidator();

        var result = await validator.ValidateAsync(ValidCommand() with { Plate = plate, Unit = unit });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Domain_preserves_optional_title_and_requires_numeric_postal_code()
    {
        var address = UserAddress.Create(
            Guid.NewGuid(), string.Empty, 8, 301, "تهران، خیابان نمونه", "1234567890",
            "کاربر نمونه", "09120000000", "12", "2");

        Assert.Equal(string.Empty, address.Title);
        Assert.ThrowsAny<Exception>(() => UserAddress.Create(
            Guid.NewGuid(), string.Empty, 8, 301, "تهران، خیابان نمونه", "12345A7890",
            "کاربر نمونه", "09120000000", "12", "2"));
    }

    private static AddAddressCommand ValidCommand() => new(
        Guid.NewGuid(),
        "خانه",
        8,
        301,
        "تهران، خیابان نمونه",
        "1234567890",
        "کاربر نمونه",
        "09120000000",
        "12",
        "2",
        null,
        null,
        false);
}
