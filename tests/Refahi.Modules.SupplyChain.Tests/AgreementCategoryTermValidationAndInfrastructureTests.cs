using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.SupplyChain.Api.Endpoints.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Api.Endpoints.AgreementProducts;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Tests;

public sealed class AgreementCategoryTermValidationAndInfrastructureTests
{
    [Fact]
    public void Add_validator_rejects_invalid_values_with_persian_messages()
    {
        var result = new AddAgreementCategoryTermCommandValidator().Validate(
            new AddAgreementCategoryTermCommand(Guid.Empty, 0, 0, 100.123m)
        );

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            x => x.PropertyName.Contains("AgreementId") && HasPersian(x.ErrorMessage)
        );
        Assert.Contains(
            result.Errors,
            x => x.PropertyName.Contains("CategoryId") && HasPersian(x.ErrorMessage)
        );
        Assert.Contains(
            result.Errors,
            x => x.PropertyName.Contains("AllowedSalesChannels") && HasPersian(x.ErrorMessage)
        );
        Assert.Contains(
            result.Errors,
            x => x.PropertyName.Contains("CommissionPercent") && HasPersian(x.ErrorMessage)
        );
    }

    [Fact]
    public void Ef_model_has_required_schema_precision_relationship_and_lookup_indexes()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(
            "Refahi.Modules.SupplyChain.Domain.Entities.AgreementCategoryTerm"
        );

        Assert.NotNull(entity);
        Assert.Equal("agreement_category_terms", entity!.GetTableName());
        Assert.Equal("supplychain", entity.GetSchema());
        Assert.False(entity.FindProperty("CategoryId")!.IsNullable);
        Assert.Equal("numeric(5,2)", entity.FindProperty("CommissionPercent")!.GetColumnType());
        Assert.Contains(
            entity.GetIndexes(),
            x => x.GetDatabaseName() == "IX_agreement_category_terms_effective_lookup"
        );
        Assert.Contains(
            entity.GetForeignKeys(),
            x => x.PrincipalEntityType.ClrType.Name == "Agreement"
        );
    }

    [Fact]
    public void Migration_chain_generates_empty_database_script_and_snapshot_is_current()
    {
        using var context = CreateContext();
        var migrations = context.Database.GetMigrations().ToList();
        var script = context
            .GetService<IMigrator>()
            .GenerateScript(fromMigration: null, toMigration: null);

        Assert.Contains(migrations, x => x.EndsWith("SupplyChain_AddAgreementCategoryTerms"));
        Assert.Contains(
            "CREATE TABLE supplychain.agreement_category_terms",
            script,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("numeric(5,2)", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "IX_agreement_category_terms_effective_lookup",
            script,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void New_admin_endpoints_have_route_name_tag_auth_cancellation_and_response_wrapper_metadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMediatR(typeof(AddAgreementCategoryTermCommand).Assembly);
        var app = builder.Build();
        new AddAgreementCategoryTermEndpoint().Map(app);
        new UpdateAgreementCategoryTermEndpoint().Map(app);
        new RemoveAgreementCategoryTermEndpoint().Map(app);

        var endpoints = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.Equal(3, endpoints.Count);
        Assert.All(
            endpoints,
            endpoint =>
            {
                Assert.Contains("agreement-category-terms", endpoint.RoutePattern.RawText);
                Assert.NotNull(
                    endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                );
                Assert.Contains(
                    endpoint.Metadata.GetOrderedMetadata<ITagsMetadata>().SelectMany(x => x.Tags),
                    x => x == "SupplyChain.AgreementCategoryTerms"
                );
                Assert.Contains(
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                    x => x.Policy == "AdminOnly"
                );
                Assert.Contains(
                    endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
                    x =>
                        x.Type?.IsGenericType == true
                        && x.Type.GetGenericTypeDefinition() == typeof(ApiResponse<>)
                );
                Assert.Contains(
                    endpoint.Metadata.GetMetadata<MethodInfo>()!.GetParameters(),
                    x => x.ParameterType == typeof(CancellationToken)
                );
            }
        );
    }

    [Fact]
    public void Legacy_agreement_product_contract_and_endpoints_are_obsolete_not_removed()
    {
        Assert.NotNull(
            typeof(AgreementProductDto)
                .GetCustomAttributes(typeof(ObsoleteAttribute), false)
                .SingleOrDefault()
        );
        Assert.NotNull(
            typeof(AddAgreementProductEndpoint)
                .GetCustomAttributes(typeof(ObsoleteAttribute), false)
                .SingleOrDefault()
        );
        Assert.NotNull(
            typeof(UpdateAgreementProductEndpoint)
                .GetCustomAttributes(typeof(ObsoleteAttribute), false)
                .SingleOrDefault()
        );
        Assert.NotNull(
            typeof(RemoveAgreementProductEndpoint)
                .GetCustomAttributes(typeof(ObsoleteAttribute), false)
                .SingleOrDefault()
        );
    }

    private static SupplyChainDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SupplyChainDbContext>()
            .UseNpgsql("Host=localhost;Database=refahi_test;Username=refahi;Password=refahi")
            .Options;
        return new SupplyChainDbContext(options);
    }

    private static bool HasPersian(string value) => value.Any(c => c >= '\u0600' && c <= '\u06ff');
}
