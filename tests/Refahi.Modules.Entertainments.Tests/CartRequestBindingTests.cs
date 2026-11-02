using System.Text.Json;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;
using Xunit;

namespace Refahi.Modules.Entertainments.Tests;

public sealed class CartRequestBindingTests
{
    [Theory]
    [InlineData("ef156693d0a142ca932cd0aa7d5535d1", true)]
    [InlineData("ef156693-d0a1-42ca-932c-d0aa7d5535d1", true)]
    [InlineData("invalid", false)]
    [InlineData("00000000000000000000000000000000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Cart_json_accepts_catalogue_keys_and_validates_program_identity(string? programKey, bool valid)
    {
        var json = JsonSerializer.Serialize(new
        {
            programKey,
            offerKey = "service:42b8c1bd-b7d8-4968-8ae2-502b9fef1207",
            purchaseOptionKey = "b33fedf6c93f3b878cca7565a79787eab8f04914e47158402edfc432a60ce83e",
            quantity = 1,
            expectedUnitPriceMinor = 2962500
        });
        var body = JsonSerializer.Deserialize<AddCartItemEndpoint.AddCartItemBody>(json, JsonSerializerOptions.Web)!;
        var command = new AddCustomerCartItemCommand(Guid.NewGuid(), body.ProgramKey, body.OfferKey,
            body.PurchaseOptionKey, body.Quantity, body.ExpectedUnitPriceMinor);

        var result = new AddCustomerCartItemValidator().Validate(command);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            Assert.Contains(result.Errors, x => x.PropertyName == nameof(command.ProgramKey) && x.ErrorMessage == "خدمت معتبر نیست");
    }
}
