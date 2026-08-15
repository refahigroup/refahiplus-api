using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class VoucherSourceDomainTests
{
    [Fact]
    public void Generated_source_accepts_positive_optional_validity()
    {
        var source = VoucherSource.Create(Guid.NewGuid(), "منبع تست",
            VoucherSourceType.Generated, VoucherRedemptionMode.RefahiValidation, 30,
            DateTimeOffset.UtcNow);

        Assert.Equal(30, source.DefaultValidityDays);
        Assert.True(source.IsActive);
    }

    [Fact]
    public void Preloaded_source_rejects_default_validity()
    {
        var ex = Assert.Throws<StoreDomainException>(() => VoucherSource.Create(
            Guid.NewGuid(), "منبع تست", VoucherSourceType.Preloaded,
            VoucherRedemptionMode.SupplierExternalValidation, 10, DateTimeOffset.UtcNow));

        Assert.Equal("INVALID_VOUCHER_SOURCE_VALIDITY", ex.ErrorCode);
    }

    [Fact]
    public void Assigned_preloaded_code_can_never_be_released_or_disabled()
    {
        var code = VoucherSourceCode.Register(Guid.NewGuid(), Guid.NewGuid(),
            new string('A', 64), "protected-value", DateTimeOffset.UtcNow, null);
        code.Reserve();
        code.Assign();

        Assert.Throws<StoreDomainException>(() => code.Release());
        Assert.Throws<StoreDomainException>(() => code.Disable());
        Assert.Equal(VoucherSourceCodeStatus.Assigned, code.Status);
    }

    [Fact]
    public void Source_code_entity_has_no_plaintext_property()
    {
        var names = typeof(VoucherSourceCode).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x => x.Contains("Plaintext", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nameof(VoucherSourceCode.CodeHash), names);
        Assert.Contains(nameof(VoucherSourceCode.CodeCiphertext), names);
    }
}
