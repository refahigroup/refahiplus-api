using System.Reflection;
using System.Security.Cryptography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Store.Api.Endpoints.Vouchers;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Features.Vouchers;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class VoucherLifecycleTests
{
    [Fact]
    public async Task PaidVoucherItem_IssuesQuantityAndReplayIsIdempotent()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, quantity: 3);
        var vouchers = new FakeVoucherRepository();
        var handler = Issuer(order, vouchers, new TestProtector());
        var paid = Paid(order);

        await handler.Handle(paid, default);
        await handler.Handle(paid, default);

        Assert.Equal(3, vouchers.Values.Count);
        Assert.Equal(new[] { 1, 2, 3 }, vouchers.Values.Select(x => x.SequenceNumber));
        Assert.Equal(3, vouchers.Values.Select(x => x.CodeHash).Distinct().Count());
        Assert.All(vouchers.Values, voucher => Assert.StartsWith("protected:", voucher.CodeCiphertext));
    }

    [Fact]
    public async Task PaidNonVoucherItem_DoesNotIssueVoucher()
    {
        var order = StoreOrderWith(FulfillmentMethod.Shipping, 2);
        var vouchers = new FakeVoucherRepository();
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        Assert.Empty(vouchers.Values);
    }

    [Fact]
    public async Task ProtectorFailure_LeavesSequenceRetryable()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var vouchers = new FakeVoucherRepository();
        await Assert.ThrowsAsync<CryptographicException>(() =>
            Issuer(order, vouchers, new FailingProtector()).Handle(Paid(order), default));
        Assert.Empty(vouchers.Values);
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        Assert.Single(vouchers.Values);
    }

    [Fact]
    public void VoucherDomain_EnforcesRedeemRevokeAndExpiryTransitions()
    {
        var now = DateTimeOffset.UtcNow;
        var issued = DirectVoucher(now);
        issued.Redeem(Guid.NewGuid(), Guid.NewGuid(), "شعبه استفاده", now.AddMinutes(1));
        Assert.Equal(VoucherStatus.Redeemed, issued.Status);
        Assert.Equal("REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE",
            Assert.Throws<StoreDomainException>(() => issued.RevokeForRefund("refund", now)).ErrorCode);

        var expiring = DirectVoucher(now, now.AddSeconds(1));
        Assert.True(expiring.ExpireIfNeeded(now.AddSeconds(1)));
        Assert.Equal(VoucherStatus.Expired, expiring.Status);
    }

    [Fact]
    public async Task Redeem_IsIdempotentAndDifferentKeyConflicts()
    {
        var supplier = Guid.NewGuid(); var shopId = Guid.NewGuid(); var vendor = Guid.NewGuid();
        var shop = Shop.Create("حضوری", "voucher-shop", ShopType.InPerson, supplier); shop.Approve();
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1, supplier, shopId);
        var protector = new TestProtector(); var repo = new FakeVoucherRepository();
        await Issuer(order, repo, protector).Handle(Paid(order), default);
        var code = protector.Plaintexts.Single();
        var handler = new RedeemVoucherHandler(repo,
            new FakeShopRepository(shop, shopId), new AuthorizationMediator(true), TimeProvider.System);

        var first = await handler.Handle(new(vendor, shopId, code, "redeem-key"), default);
        var retry = await handler.Handle(new(vendor, shopId, code, "redeem-key"), default);

        Assert.Equal(first, retry);
        var ownerDetail = await new GetMyVouchersHandler(repo, protector).Handle(
            new GetMyVoucherQuery(order.UserId, first.VoucherId), default);
        Assert.NotNull(ownerDetail);
        Assert.Equal(shopId, ownerDetail.RedeemedShopId);
        Assert.Equal("حضوری", ownerDetail.RedeemedShopName);
        var ex = await Assert.ThrowsAsync<VoucherApplicationException>(() =>
            handler.Handle(new(vendor, shopId, code, "different-key"), default));
        Assert.Equal("VOUCHER_ALREADY_REDEEMED", ex.Code);
    }

    [Fact]
    public async Task Redeem_RejectsWrongShopAndUnauthorizedUserWithoutCodeLeak()
    {
        var supplier = Guid.NewGuid(); var shopId = Guid.NewGuid();
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1, supplier, shopId);
        var protector = new TestProtector(); var repo = new FakeVoucherRepository();
        await Issuer(order, repo, protector).Handle(Paid(order), default);
        var code = protector.Plaintexts.Single();
        var wrongShop = Shop.Create("غلط", "wrong-shop", ShopType.InPerson, Guid.NewGuid()); wrongShop.Approve();
        var wrong = new RedeemVoucherHandler(repo,
            new FakeShopRepository(wrongShop, shopId), new AuthorizationMediator(true), TimeProvider.System);
        var wrongEx = await Assert.ThrowsAsync<VoucherApplicationException>(() =>
            wrong.Handle(new(Guid.NewGuid(), shopId, code, "key-1"), default));
        Assert.Equal("VOUCHER_NOT_REDEEMABLE", wrongEx.Code);
        Assert.DoesNotContain(code, wrongEx.Message, StringComparison.Ordinal);

        var validShop = Shop.Create("صحیح", "valid-shop", ShopType.InPerson, supplier); validShop.Approve();
        var denied = new RedeemVoucherHandler(repo,
            new FakeShopRepository(validShop, shopId), new AuthorizationMediator(false), TimeProvider.System);
        Assert.Equal("VOUCHER_REDEEM_FORBIDDEN", (await Assert.ThrowsAsync<VoucherApplicationException>(() =>
            denied.Handle(new(Guid.NewGuid(), shopId, code, "key-2"), default))).Code);
    }

    [Fact]
    public async Task OwnerReadUnprotectsButAuditContractHasNoSecretProperties()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var protector = new TestProtector(); var repo = new FakeVoucherRepository();
        await Issuer(order, repo, protector).Handle(Paid(order), default);
        var reads = new GetMyVouchersHandler(repo, protector);
        using var cts = new CancellationTokenSource();
        var ownerVoucher = Assert.Single(await reads.Handle(new GetMyVouchersQuery(order.UserId), cts.Token));
        Assert.Equal(order.OrderId, ownerVoucher.OrderId);
        Assert.Equal("SO-1", ownerVoucher.OrderNumber);
        Assert.Equal(order.SupplierId, ownerVoucher.SupplierId);
        Assert.Equal("تامین‌کننده تست", ownerVoucher.SupplierName);
        Assert.Equal(order.ShopId, ownerVoucher.ShopId);
        Assert.Equal("فروشگاه تست", ownerVoucher.ShopName);
        Assert.Equal("محصول ووچری", ownerVoucher.ProductTitle);
        Assert.Equal(1, repo.UserReadCount);
        Assert.Equal(cts.Token, repo.LastCancellationToken);
        Assert.Null(await reads.Handle(new GetMyVoucherQuery(Guid.NewGuid(), repo.Values[0].Id), default));
        var auditNames = typeof(VoucherAuditDto).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("CodeHash", auditNames);
        Assert.DoesNotContain("CodeCiphertext", auditNames);
        Assert.DoesNotContain("Code", auditNames);
        var ownerNames = typeof(VoucherDto).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("CodeHash", ownerNames);
        Assert.DoesNotContain("CodeCiphertext", ownerNames);
    }

    [Fact]
    public async Task OwnerRead_ProtectorFailureReturnsTypedError()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var issueProtector = new TestProtector(); var repo = new FakeVoucherRepository();
        await Issuer(order, repo, issueProtector).Handle(Paid(order), default);
        var reads = new GetMyVouchersHandler(repo, new FailingProtector());
        var ex = await Assert.ThrowsAsync<VoucherApplicationException>(() =>
            reads.Handle(new GetMyVouchersQuery(order.UserId), default));
        Assert.Equal("VOUCHER_CODE_UNAVAILABLE", ex.Code);
    }

    [Fact]
    public async Task RedemptionHistory_IsFailClosedForUnauthorizedVendorScope()
    {
        var handler = new GetVendorVoucherRedemptionHistoryHandler(new FakeVoucherRepository(),
            new AuthorizationMediator(false));
        var ex = await Assert.ThrowsAsync<VoucherApplicationException>(() => handler.Handle(
            new(Guid.NewGuid(), Guid.NewGuid(), null), default));
        Assert.Equal("VOUCHER_HISTORY_FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task RedemptionHistory_IsScopedPaginatedAndContainsOnlySafeSnapshots()
    {
        var supplier = Guid.NewGuid();
        var shopOne = Guid.NewGuid(); var shopTwo = Guid.NewGuid();
        var repo = new FakeVoucherRepository();
        var older = await AddRedeemedVoucher(repo, supplier, shopOne, "شعبه یک", "محصول یک", 1,
            DateTimeOffset.Parse("2026-08-09T08:00:00Z"));
        var newer = await AddRedeemedVoucher(repo, supplier, shopTwo, "شعبه دو", "محصول دو", 2,
            DateTimeOffset.Parse("2026-08-09T09:00:00Z"));
        await AddRedeemedVoucher(repo, Guid.NewGuid(), Guid.NewGuid(), "خارج از محدوده", "محصول دیگر", 1,
            DateTimeOffset.Parse("2026-08-09T10:00:00Z"));
        var handler = new GetVendorVoucherRedemptionHistoryHandler(repo, new AuthorizationMediator(true));
        using var cts = new CancellationTokenSource();

        var first = await handler.Handle(new(Guid.NewGuid(), supplier, null, 1, 1), cts.Token);
        Assert.Equal(cts.Token, repo.LastHistoryCancellationToken);
        var second = await handler.Handle(new(Guid.NewGuid(), supplier, null, 2, 1), default);
        var shopScoped = await handler.Handle(new(Guid.NewGuid(), supplier, shopOne, 1, 20), default);

        Assert.Equal(2, first.Total);
        Assert.Equal(1, first.Page); Assert.Equal(1, first.PageSize);
        Assert.Equal(newer.Id, Assert.Single(first.Items).VoucherId);
        Assert.Equal(older.Id, Assert.Single(second.Items).VoucherId);
        var scoped = Assert.Single(shopScoped.Items);
        Assert.Equal(1, shopScoped.Total);
        Assert.Equal(shopOne, scoped.ShopId);
        Assert.Equal("شعبه یک", scoped.ShopName);
        Assert.Equal("محصول یک", scoped.ProductTitle);
        Assert.StartsWith("VCH-****-0001-", scoped.MaskedReference);
        Assert.Equal(3, repo.HistoryReadCount);
        var names = typeof(VoucherRedemptionHistoryItemDto).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x => x.Contains("Code", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("Cipher", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RedemptionHistory_ValidatesPaginationBounds()
    {
        var validator = new GetVendorVoucherRedemptionHistoryValidator();
        var query = new GetVendorVoucherRedemptionHistoryQuery(
            Guid.NewGuid(), Guid.NewGuid(), null, 0, 101);
        var result = validator.Validate(query);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetVendorVoucherRedemptionHistoryQuery.Page));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetVendorVoucherRedemptionHistoryQuery.PageSize));
    }

    [Fact]
    public async Task RefundRevokesIssuedAndBlocksRedeemedBeforePaymentCoordination()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 2);
        var repo = new FakeVoucherRepository(); var protector = new TestProtector();
        await Issuer(order, repo, protector).Handle(Paid(order), default);
        var handler = new PrepareStoreOrderRefundHandler(new FakeStoreOrderRepository(order), repo,
            new FakeVoucherRefundOverrideRepository(), new NoopMutationLock(), TimeProvider.System);
        var result = await handler.Handle(new(order.OrderId!.Value, "refund"), default);
        Assert.Equal(2, result.RevokedCount);
        Assert.All(repo.Values, x => Assert.Equal(VoucherStatus.Revoked, x.Status));
        Assert.Equal(0, (await handler.Handle(new(order.OrderId.Value, "refund"), default)).RevokedCount);

        var redeemedOrder = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var redeemedRepo = new FakeVoucherRepository(); var redeemedProtector = new TestProtector();
        await Issuer(redeemedOrder, redeemedRepo, redeemedProtector).Handle(Paid(redeemedOrder), default);
        redeemedRepo.Values[0].Redeem(Guid.NewGuid(), redeemedOrder.ShopId, "شعبه استفاده", DateTimeOffset.UtcNow);
        var blocked = new PrepareStoreOrderRefundHandler(new FakeStoreOrderRepository(redeemedOrder),
            redeemedRepo, new FakeVoucherRefundOverrideRepository(), new NoopMutationLock(), TimeProvider.System);
        Assert.Equal("REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE",
            (await Assert.ThrowsAsync<VoucherApplicationException>(() => blocked.Handle(
                new(redeemedOrder.OrderId!.Value, "refund"), default))).Code);
    }

    [Fact]
    public async Task AuthorizedOverride_PersistsAudit_CompletesOnce_AndKeepsVoucherRedeemed()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var vouchers = new FakeVoucherRepository();
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        vouchers.Values[0].Redeem(Guid.NewGuid(), order.ShopId, "شعبه", DateTimeOffset.UtcNow);
        order.MarkPaid();
        var overrides = new FakeVoucherRefundOverrideRepository();
        var mediator = new RefundOverrideMediator();
        var handler = OverrideHandler(order, vouchers, overrides, mediator);
        var admin = Guid.NewGuid();
        var command = new OverrideRedeemedVoucherRefundCommand(order.OrderId!.Value, admin,
            "سرویس قبلاً ارائه شده و بازگشت وجه با تایید مدیر انجام می‌شود", "override-key");

        var first = await handler.Handle(command, default);
        var retry = await handler.Handle(command, default);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("RefundCompleted", retry.Outcome);
        Assert.Equal(admin, first.AdminUserId);
        Assert.Equal(1, mediator.CancelCount);
        Assert.Single(overrides.Values);
        Assert.Single(overrides.Attempts);
        Assert.Equal(VoucherStatus.Redeemed, vouchers.Values[0].Status);
        Assert.DoesNotContain("Code", overrides.Values[0].VoucherSnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedRefund_KeepsAuditAndRetriesSameDeterministicRefund()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var vouchers = new FakeVoucherRepository();
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        vouchers.Values[0].Redeem(Guid.NewGuid(), order.ShopId, "شعبه", DateTimeOffset.UtcNow);
        order.MarkPaid();
        var overrides = new FakeVoucherRefundOverrideRepository();
        var mediator = new RefundOverrideMediator(failuresBeforeSuccess: 1);
        var handler = OverrideHandler(order, vouchers, overrides, mediator);
        var command = new OverrideRedeemedVoucherRefundCommand(order.OrderId!.Value, Guid.NewGuid(),
            "خطای ارائه سرویس تایید شده و بازگشت وجه استثنایی لازم است", "retryable-key");

        var failed = await handler.Handle(command, default);
        var completed = await handler.Handle(command, default);

        Assert.Equal("ReconciliationRequired", failed.Outcome);
        Assert.Equal("RefundCompleted", completed.Outcome);
        Assert.Equal(failed.Id, completed.Id);
        Assert.Equal(2, mediator.CancelCount);
        Assert.Single(mediator.IdempotencyKeys.Distinct());
        Assert.Equal(2, overrides.Attempts.Count);
    }

    [Fact]
    public async Task OverrideIdempotency_RejectsChangedPayload_AndConcurrentAdminReusesAudit()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var vouchers = new FakeVoucherRepository();
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        vouchers.Values[0].Redeem(Guid.NewGuid(), order.ShopId, "شعبه", DateTimeOffset.UtcNow);
        order.MarkPaid();
        var overrides = new FakeVoucherRefundOverrideRepository();
        var mediator = new RefundOverrideMediator();
        var handler = OverrideHandler(order, vouchers, overrides, mediator);
        var first = await handler.Handle(new(order.OrderId!.Value, Guid.NewGuid(),
            "دلیل معتبر برای استثنای بازگشت وجه ثبت شد", "same-key"), default);

        var mismatch = await Assert.ThrowsAsync<VoucherApplicationException>(() => handler.Handle(
            new(order.OrderId.Value, Guid.NewGuid(), "دلیل متفاوت و معتبر برای استثنا", "same-key"), default));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", mismatch.Code);

        var secondAdmin = await handler.Handle(new(order.OrderId.Value, Guid.NewGuid(),
            first.Reason, "another-admin-key"), default);
        Assert.Equal(first.Id, secondAdmin.Id);
        Assert.Single(overrides.Values);
    }

    [Fact]
    public async Task ValidOverride_AllowsRefundGuardWithoutUndoingRedeemedVoucher()
    {
        var order = StoreOrderWith(FulfillmentMethod.Voucher, 1);
        var vouchers = new FakeVoucherRepository();
        await Issuer(order, vouchers, new TestProtector()).Handle(Paid(order), default);
        vouchers.Values[0].Redeem(Guid.NewGuid(), order.ShopId, "شعبه", DateTimeOffset.UtcNow);
        order.MarkPaid();
        var overrides = new FakeVoucherRefundOverrideRepository();
        var reason = "بازگشت وجه استثنایی با تایید مدیر سامانه";
        var snapshot = System.Text.Json.JsonSerializer.Serialize(vouchers.Values.Select(x =>
            new VoucherRefundSnapshotItemDto(x.Id, x.Status.ToString())));
        var value = VoucherRefundOverride.Create(order.Id, order.OrderId!.Value, Guid.NewGuid(),
            reason, snapshot, "guard-key", new string('a', 64), Guid.NewGuid(), DateTimeOffset.UtcNow);
        await overrides.AddAsync(value);
        var handler = new PrepareStoreOrderRefundHandler(new FakeStoreOrderRepository(order), vouchers,
            overrides, new NoopMutationLock(), TimeProvider.System);

        var result = await handler.Handle(new(order.OrderId.Value, reason, value.Id), default);

        Assert.Equal(0, result.RevokedCount);
        Assert.Equal(VoucherStatus.Redeemed, vouchers.Values[0].Status);
    }

    [Fact]
    public void OverrideValidator_RequiresMeaningfulTrimmedReason()
    {
        var validator = new OverrideRedeemedVoucherRefundValidator();
        var invalid = new OverrideRedeemedVoucherRefundCommand(
            Guid.NewGuid(), Guid.NewGuid(), " کوتاه ", "key");
        var valid = new OverrideRedeemedVoucherRefundCommand(Guid.NewGuid(), Guid.NewGuid(),
            "  دلیل معتبر برای بازگشت وجه استثنایی  ", "key");
        Assert.False(validator.Validate(invalid).IsValid);
        Assert.True(validator.Validate(valid).IsValid);
    }

    private static OverrideRedeemedVoucherRefundHandler OverrideHandler(StoreOrder order,
        FakeVoucherRepository vouchers, FakeVoucherRefundOverrideRepository overrides, IMediator mediator) => new(
        new FakeStoreOrderRepository(order), vouchers, overrides, new NoopMutationLock(), mediator,
        TimeProvider.System, NullLogger<OverrideRedeemedVoucherRefundHandler>.Instance);

    [Fact]
    public void EfModel_HasVoucherIndexesConcurrencyAndNoPlaintextColumn()
    {
        using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=model;Username=model;Password=model").Options);
        var voucher = db.Model.FindEntityType(typeof(Voucher))!;
        Assert.Equal("vouchers", voucher.GetTableName());
        Assert.True(voucher.FindProperty(nameof(Voucher.Version))!.IsConcurrencyToken);
        Assert.Null(voucher.FindProperty("Code"));
        Assert.Contains(voucher.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(Voucher.StoreOrderItemId), nameof(Voucher.SequenceNumber)]));
        Assert.Contains(voucher.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Voucher.CodeHash));
    }

    [Fact]
    public void VoucherEndpoints_HaveRequiredPoliciesAndRateLimitMetadata()
    {
        var builder = WebApplication.CreateBuilder(); builder.Services.AddRouting();
        builder.Services.AddScoped<VoucherTypedErrorFilter>();
        builder.Services.AddSingleton<IMediator>(new AuthorizationMediator(true));
        var app = builder.Build(); new VoucherEndpoints().Map(app.MapGroup("/api/store"));
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Where(x => x.RoutePattern.RawText?.Contains("vouchers") == true).ToArray();
        Assert.Equal(5, endpoints.Length);
        Assert.Contains(endpoints, x => x.RoutePattern.RawText!.Contains("redeem") &&
            x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "VendorOnly") &&
            x.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "VoucherRedeem");
        Assert.Contains(endpoints, x => x.RoutePattern.RawText!.Contains("history") &&
            x.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "VoucherRedeem");
        Assert.Contains(endpoints, x => x.RoutePattern.RawText!.Contains("admin") &&
            x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "AdminOnly"));
        var allEndpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        Assert.Contains(allEndpoints, x =>
            x.RoutePattern.RawText!.Contains("voucher-refund-override") &&
            x.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "AdminOnly"));
    }

    private static IssueVouchersAfterOrderPaidHandler Issuer(StoreOrder order, FakeVoucherRepository repo,
        IVoucherCodeProtector protector)
    {
        var shop = Shop.Create("فروشگاه تست", "voucher-issue-shop", ShopType.InPerson, order.SupplierId);
        return new(new FakeStoreOrderRepository(order), repo, new FakeShopRepository(shop, order.ShopId),
            new AuthorizationMediator(true), protector,
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<IssueVouchersAfterOrderPaidHandler>.Instance);
    }

    private static Voucher DirectVoucher(DateTimeOffset now, DateTimeOffset? expires = null) =>
        Voucher.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-DIRECT", 1,
            Guid.NewGuid(), Guid.NewGuid(), "تامین‌کننده", Guid.NewGuid(), "فروشگاه",
            Guid.NewGuid(), "محصول", new string('A', 64), "cipher", now, expires);

    private static async Task<Voucher> AddRedeemedVoucher(FakeVoucherRepository repo, Guid supplierId,
        Guid shopId, string shopName, string productTitle, int sequence, DateTimeOffset redeemedAt)
    {
        var voucher = Voucher.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-HISTORY", sequence,
            Guid.NewGuid(), supplierId, "تامین‌کننده", shopId, shopName, Guid.NewGuid(), productTitle,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), "protected-secret", redeemedAt.AddMinutes(-5));
        var vendor = Guid.NewGuid();
        voucher.Redeem(vendor, shopId, shopName, redeemedAt);
        var redemption = VoucherRedemption.Create(voucher.Id, vendor, supplierId, shopId,
            Guid.NewGuid().ToString("N"), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), redeemedAt);
        await repo.AddAsync(voucher);
        await repo.RedeemAsync(voucher, redemption);
        return voucher;
    }

    private static OrderPaidIntegrationEvent Paid(StoreOrder order) => new(order.OrderId!.Value, "SO-1",
        order.UserId, "Store", order.Id, "StoreOrder", null, Guid.NewGuid(), order.FinalAmountMinor,
        DateTimeOffset.UtcNow);

    private static StoreOrder StoreOrderWith(FulfillmentMethod fulfillment, int quantity,
        Guid? supplier = null, Guid? shop = null)
    {
        var supplierId = supplier ?? Guid.NewGuid(); var shopId = shop ?? Guid.NewGuid();
        var snapshot = new StoreOrderItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(),
            "محصول ووچری", null, null, 10, "store.voucher", supplierId, shopId,
            SalesChannel.Online, ProductType.Service, SalesModel.Unlimited, fulfillment,
            quantity, 100_000, 0, 100_000, Guid.NewGuid(), Guid.NewGuid(), 10, null, 0);
        var order = StoreOrder.Create(Guid.NewGuid(), 1, shopId, supplierId, "voucher-order",
            new string('c', 64), [snapshot], Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), 1);
        order.AttachOrder(Guid.NewGuid());
        return order;
    }

    private sealed class TestProtector : IVoucherCodeProtector
    {
        public List<string> Plaintexts { get; } = [];
        public string Protect(string value) { Plaintexts.Add(value); return "protected:" + value; }
        public bool TryUnprotect(string value, out string plaintext)
        { plaintext = value.StartsWith("protected:") ? value[10..] : string.Empty; return plaintext.Length > 0; }
    }
    private sealed class FailingProtector : IVoucherCodeProtector
    {
        public string Protect(string value) => throw new CryptographicException("key unavailable");
        public bool TryUnprotect(string value, out string plaintext) { plaintext = ""; return false; }
    }

    private sealed class FakeVoucherRepository : IVoucherRepository
    {
        public List<Voucher> Values { get; } = [];
        public int UserReadCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public int HistoryReadCount { get; private set; }
        public CancellationToken LastHistoryCancellationToken { get; private set; }
        private readonly List<VoucherRedemption> redemptions = [];
        public Task<Voucher?> GetByItemSequenceAsync(Guid item, int sequence, CancellationToken ct = default) =>
            Task.FromResult(Values.SingleOrDefault(x => x.StoreOrderItemId == item && x.SequenceNumber == sequence));
        public Task<Voucher?> GetByCodeHashAsync(string hash, CancellationToken ct = default) => Task.FromResult(Values.SingleOrDefault(x => x.CodeHash == hash));
        public Task<Voucher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Voucher>> GetByUserAsync(Guid id, CancellationToken ct = default)
        {
            UserReadCount++;
            LastCancellationToken = ct;
            return Task.FromResult<IReadOnlyList<Voucher>>(Values.Where(x => x.UserId == id).ToArray());
        }
        public Task<IReadOnlyList<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Voucher>>(Values);
        public Task<IReadOnlyList<Voucher>> GetByStoreOrderAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Voucher>>(Values.Where(x => x.StoreOrderId == id).ToArray());
        public Task<VoucherRedemption?> GetRedemptionByIdempotencyAsync(Guid user, string key, CancellationToken ct = default) => Task.FromResult(redemptions.SingleOrDefault(x => x.VendorUserId == user && x.IdempotencyKey == key));
        public Task<VoucherRedemptionHistoryPage> GetRedemptionHistoryAsync(Guid supplier, Guid? shop,
            int page, int pageSize, CancellationToken ct = default)
        {
            HistoryReadCount++;
            LastHistoryCancellationToken = ct;
            var query = from redemption in redemptions
                        join voucher in Values on redemption.VoucherId equals voucher.Id
                        where redemption.SupplierId == supplier && voucher.SupplierId == supplier &&
                              (!shop.HasValue || redemption.ShopId == shop.Value)
                        orderby redemption.RedeemedAtUtc descending, voucher.Id
                        select new VoucherRedemptionHistoryRow(voucher.Id, voucher.StoreOrderId,
                            voucher.SequenceNumber, voucher.ProductId, voucher.ProductTitle, supplier,
                            redemption.ShopId, voucher.RedeemedShopName ?? voucher.ShopName,
                            redemption.VendorUserId, redemption.RedeemedAtUtc);
            var rows = query.ToArray();
            return Task.FromResult(new VoucherRedemptionHistoryPage(rows.Length,
                rows.Skip((page - 1) * pageSize).Take(pageSize).ToArray()));
        }
        public Task AddAsync(Voucher value, CancellationToken ct = default) { Values.Add(value); return Task.CompletedTask; }
        public Task RedeemAsync(Voucher value, VoucherRedemption redemption, CancellationToken ct = default) { redemptions.Add(redemption); return Task.CompletedTask; }
        public Task UpdateAsync(Voucher value, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRangeAsync(IReadOnlyCollection<Voucher> values, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStoreOrderRepository(StoreOrder value) : IStoreOrderRepository
    {
        public Task<StoreOrder?> GetByIdempotencyKeyAsync(Guid user, string key, CancellationToken ct = default) => Task.FromResult<StoreOrder?>(null);
        public Task<StoreOrder?> GetByOrderIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(value.OrderId == id ? value : null);
        public Task<StoreOrder?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(value.Id == id ? value : null);
        public Task<IReadOnlyList<StoreOrder>> GetByOrderIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StoreOrder>>(ids.Contains(value.OrderId!.Value) ? [value] : []);
        public Task AddAsync(StoreOrder order, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(StoreOrder order, CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitPaidAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeShopRepository(Shop value, Guid expectedId) : IShopRepository
    {
        public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == expectedId ? value : null);
        public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Shop?> GetByProviderIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Shop>> GetBySupplierIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedAsync(ShopType? type, ShopStatus? status, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ProviderHasShopAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedByIdsAsync(IEnumerable<Guid> ids, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Shop>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Shop shop, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Shop shop, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AuthorizationMediator(bool allowed) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object? result = request switch
            {
                AuthorizeStoreResourceQuery => allowed,
                GetSupplierByIdQuery supplier => Supplier(supplier.Id),
                _ => throw new NotSupportedException()
            };
            return Task.FromResult((TResponse)result!);
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();

        private static SupplierDto Supplier(Guid id) => new(id, 1, "حقوقی", null, null,
            "شرکت تست", "تامین‌کننده تست", null, null, null, null, null, null, null,
            null, null, null, null, null, 3, "تاییدشده", null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
    }

    private sealed class NoopMutationLock : IStoreOrderMutationLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct) => Task.FromResult<IAsyncDisposable>(new Handle());
        private sealed class Handle : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    private sealed class FakeVoucherRefundOverrideRepository : IVoucherRefundOverrideRepository
    {
        public List<VoucherRefundOverride> Values { get; } = [];
        public List<VoucherRefundOverrideAttempt> Attempts { get; } = [];
        public Task<VoucherRefundOverride?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id));
        public Task<VoucherRefundOverride?> GetByOrderIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Values.SingleOrDefault(x => x.OrderId == id));
        public Task<VoucherRefundOverride?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Values.SingleOrDefault(x => x.IdempotencyKey == key));
        public Task<IReadOnlyList<VoucherRefundOverrideAttempt>> GetAttemptsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<VoucherRefundOverrideAttempt>>(Attempts.Where(x => x.VoucherRefundOverrideId == id).OrderBy(x => x.SequenceNumber).ToArray());
        public Task AddAsync(VoucherRefundOverride value, CancellationToken ct = default) { Values.Add(value); return Task.CompletedTask; }
        public Task AddAttemptAsync(VoucherRefundOverrideAttempt value, CancellationToken ct = default) { Attempts.Add(value); return Task.CompletedTask; }
    }

    private sealed class RefundOverrideMediator(int failuresBeforeSuccess = 0) : IMediator
    {
        public int CancelCount { get; private set; }
        public List<string> IdempotencyKeys { get; } = [];
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is not CancelOrderCommand command) throw new NotSupportedException();
            CancelCount++;
            IdempotencyKeys.Add(command.IdempotencyKey);
            if (CancelCount <= failuresBeforeSuccess) throw new InvalidOperationException("provider detail must not leak");
            return Task.FromResult((TResponse)(object)new CancelOrderResponse(command.OrderId, "Cancelled", "Refunded"));
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
