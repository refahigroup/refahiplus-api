using MediatR;
using Microsoft.AspNetCore.Http;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Security;

public sealed class StoreProviderOwnershipFilter(
    IShopRepository shops,
    IProductRepository products,
    IProductSessionRepository sessions,
    IMediator mediator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Path.Value?.Contains("/provider/", StringComparison.OrdinalIgnoreCase) != true ||
            http.User.IsInRole("Admin"))
            return await next(context);
        if (!Guid.TryParse(
                http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
                out var userId))
            return Results.Unauthorized();

        var supplierId = await ResolveSupplierIdAsync(context, http, http.RequestAborted);
        if (!supplierId.HasValue ||
            !await mediator.Send(new AuthorizeStoreResourceQuery(
                userId, supplierId.Value, null, StorePermissions.EditVendorProfile), http.RequestAborted))
            return Results.Json(
                ApiResponseHelper.Error("دسترسی به منبع فروشگاه وجود ندارد", statusCode: 403),
                statusCode: StatusCodes.Status403Forbidden);
        return await next(context);
    }

    private async Task<Guid?> ResolveSupplierIdAsync(
        EndpointFilterInvocationContext invocation, HttpContext http, CancellationToken ct)
    {
        var updateShop = invocation.Arguments.OfType<UpdateShopCommand>().FirstOrDefault();
        if (updateShop is not null)
            return (await shops.GetByIdAsync(updateShop.Id, ct))?.SupplierId;

        var createProduct = invocation.Arguments.OfType<CreateProductCommand>().FirstOrDefault();
        if (createProduct is not null)
            return await SupplierByAgreementProductAsync(createProduct.AgreementProductId, ct);

        Guid? productId = RouteGuid(http, "productId");
        if (!productId.HasValue && http.Request.Path.Value?.Contains("/provider/products/", StringComparison.OrdinalIgnoreCase) == true)
            productId = RouteGuid(http, "id");
        if (productId.HasValue)
        {
            var product = await products.GetByIdAsync(productId.Value, ct);
            if (product is not null)
                return await SupplierByAgreementProductAsync(product.AgreementProductId, ct);
        }

        if (http.Request.Path.Value?.Contains("/provider/sessions/", StringComparison.OrdinalIgnoreCase) == true &&
            RouteGuid(http, "id") is Guid sessionId)
        {
            var session = await sessions.GetByIdAsync(sessionId, ct);
            var product = session is null ? null : await products.GetByIdAsync(session.ProductId, ct);
            if (product is not null)
                return await SupplierByAgreementProductAsync(product.AgreementProductId, ct);
        }
        return null;
    }

    private async Task<Guid?> SupplierByAgreementProductAsync(Guid agreementProductId, CancellationToken ct)
    {
        var product = await mediator.Send(new GetAgreementProductByIdQuery(agreementProductId), ct);
        if (product is null) return null;
        return (await mediator.Send(new GetAgreementByIdQuery(product.AgreementId), ct))?.SupplierId;
    }

    private static Guid? RouteGuid(HttpContext context, string key)
        => context.Request.RouteValues.TryGetValue(key, out var value) &&
           Guid.TryParse(value?.ToString(), out var id) ? id : null;
}
