using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Tests;

public sealed class AgreementCategoryTermDomainTests
{
    [Fact]
    public void Create_update_and_soft_delete_preserve_locked_business_shape()
    {
        var agreement = CreateAgreement();
        var term = agreement.AddCategoryTerm(12, SalesChannel.Online | SalesChannel.InPerson, 12.25m);

        Assert.Equal(12, term.CategoryId);
        Assert.Equal(SalesChannel.Online | SalesChannel.InPerson, term.AllowedSalesChannels);
        Assert.Equal(12.25m, term.CommissionPercent);

        agreement.UpdateCategoryTerm(term.Id, 13, SalesChannel.InPerson, 8m);
        Assert.Equal(13, term.CategoryId);
        Assert.Equal(SalesChannel.InPerson, term.AllowedSalesChannels);

        agreement.RemoveCategoryTerm(term.Id);
        Assert.True(term.IsDeleted);
    }

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(1, 0, 10)]
    [InlineData(1, 4, 10)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 1, 101)]
    public void Invalid_business_values_have_persian_domain_errors(int categoryId, short channels, decimal commission)
    {
        var ex = Assert.Throws<SupplyChainDomainException>(() =>
            CreateAgreement().AddCategoryTerm(categoryId, (SalesChannel)channels, commission));
        Assert.Contains(ex.Message, c => c >= '\u0600' && c <= '\u06ff');
    }

    [Fact]
    public void Commission_with_more_than_two_decimal_places_is_rejected_by_domain()
    {
        var ex = Assert.Throws<SupplyChainDomainException>(() =>
            CreateAgreement().AddCategoryTerm(1, SalesChannel.Online, 10.123m));
        Assert.Equal("COMMISSION_PERCENT_SCALE_INVALID", ex.ErrorCode);
    }

    [Fact]
    public void Approved_agreement_terms_are_immutable()
    {
        var agreement = CreateAgreement();
        var term = agreement.AddCategoryTerm(1, SalesChannel.Online, 5m);
        agreement.SubmitForReview();
        agreement.Approve();

        var add = Assert.Throws<SupplyChainDomainException>(() =>
            agreement.AddCategoryTerm(2, SalesChannel.Online, 5m));
        var update = Assert.Throws<SupplyChainDomainException>(() =>
            agreement.UpdateCategoryTerm(term.Id, 2, SalesChannel.Online, 5m));
        var remove = Assert.Throws<SupplyChainDomainException>(() => agreement.RemoveCategoryTerm(term.Id));

        Assert.All([add, update, remove], ex => Assert.Equal("STATUS_IMMUTABLE", ex.ErrorCode));
    }

    private static Agreement CreateAgreement() => Agreement.Create(
        "A-1", AgreementType.Normal, Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
}
