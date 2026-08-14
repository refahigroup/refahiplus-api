using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Modules.Store.Application.Features.Modules.CreateModule;
using Refahi.Modules.Store.Application.Features.Modules.UpdateModule;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class StoreModuleCategoryInvariantTests
{
    [Fact]
    public void Create_RejectsMissingRootCategory()
    {
        var exception = Assert.Throws<StoreDomainException>(() =>
            StoreModule.Create("هتل", "hotel"));

        Assert.Equal("MODULE_CATEGORY_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public void ActiveModule_CannotRemoveRootCategory()
    {
        var module = StoreModule.Create("هتل", "hotel", categoryId: 6);

        var exception = Assert.Throws<StoreDomainException>(() =>
            module.UpdateInfo("هتل", null, null, 0, null));

        Assert.Equal("MODULE_CATEGORY_REQUIRED", exception.ErrorCode);
        Assert.Equal(6, module.CategoryId);
    }

    [Fact]
    public void CreateValidator_RejectsMissingRootCategory()
    {
        var validator = new CreateModuleCommandValidator();
        var result = validator.Validate(new CreateModuleCommand(
            "هتل", "hotel", null, null, 0, null));

        Assert.Contains(result.Errors, error => error.PropertyName == "CategoryId");
    }

    [Fact]
    public void UpdateValidator_RejectsMissingRootCategory()
    {
        var validator = new UpdateModuleCommandValidator();
        var result = validator.Validate(new UpdateModuleCommand(
            5, "هتل", null, null, 0, null));

        Assert.Contains(result.Errors, error => error.PropertyName == "CategoryId");
    }
}
