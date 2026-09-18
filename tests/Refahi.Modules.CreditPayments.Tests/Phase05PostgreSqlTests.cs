using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Features.CreateOrGetExternalPaymentOrder;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Orders.Infrastructure.Repositories;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase05PostgreSqlTests : IAsyncLifetime
{
    private const string Phase04 =
        "20260916133952_CreditPayments_AddCheckoutAndCustomerAuthentication";
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 14, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Phase04_to_phase05_preserves_history_enforces_unique_order_and_rolls_back()
    {
        await using var db = Create();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Phase04);

        var merchant = Merchant.Create(Guid.NewGuid(), "Historical Merchant", Now);
        merchant.Activate(Now);
        var terminal = merchant.AddTerminal(
            "Historical Terminal",
            "store",
            100,
            1000,
            ["https://merchant.example/callback"],
            Now);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();

        var paymentA = Guid.NewGuid();
        var paymentB = Guid.NewGuid();
        await InsertHistoricalPaymentAsync(db, merchant.Id, terminal.Id, paymentA, "HIST-A");
        await InsertHistoricalPaymentAsync(db, merchant.Id, terminal.Id, paymentB, "HIST-B");
        var checkoutId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO credit_payments.checkout_sessions
(checkout_session_id, gateway_payment_id, status, token_nonce, token_hash,
 token_key_version, expires_at, created_at, updated_at, version)
VALUES ({checkoutId}, {paymentA}, {(short)CheckoutSessionStatus.Created},
 {new byte[32]}, {Enumerable.Repeat((byte)1,32).ToArray()}, {1},
 {Now.AddMinutes(15)}, {Now}, {Now}, {1L})");

        await migrator.MigrateAsync();

        await using (var connection = new NpgsqlConnection(postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(@"
SELECT gp.order_id, gp.order_bound_at,
       (SELECT count(*) FROM credit_payments.checkout_sessions WHERE checkout_session_id=@checkout_id),
       (SELECT count(*) FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
          ON tc.constraint_name=kcu.constraint_name AND tc.constraint_schema=kcu.constraint_schema
        WHERE tc.constraint_schema='credit_payments' AND tc.constraint_type='FOREIGN KEY'
          AND kcu.column_name='order_id')
FROM credit_payments.gateway_payments gp
WHERE gp.gateway_payment_id=@payment_id", connection);
            command.Parameters.AddWithValue("checkout_id", checkoutId);
            command.Parameters.AddWithValue("payment_id", paymentA);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(await reader.IsDBNullAsync(0));
            Assert.True(await reader.IsDBNullAsync(1));
            Assert.Equal(1L, reader.GetInt64(2));
            Assert.Equal(0L, reader.GetInt64(3));
        }

        var orderId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE credit_payments.gateway_payments
SET order_id={orderId}, order_bound_at={Now}
WHERE gateway_payment_id={paymentA}");
        var unique = await Assert.ThrowsAnyAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE credit_payments.gateway_payments
SET order_id={orderId}, order_bound_at={Now}
WHERE gateway_payment_id={paymentB}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, unique.SqlState);

        var check = await Assert.ThrowsAnyAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE credit_payments.gateway_payments
SET order_id={Guid.NewGuid()}, order_bound_at=NULL
WHERE gateway_payment_id={paymentB}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, check.SqlState);

        await migrator.MigrateAsync(Phase04);
        await using var verify = new NpgsqlConnection(postgres.GetConnectionString());
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(@"
SELECT count(*),
       (SELECT count(*) FROM credit_payments.checkout_sessions),
       (SELECT count(*) FROM information_schema.columns
        WHERE table_schema='credit_payments' AND table_name='gateway_payments'
          AND column_name IN ('order_id','order_bound_at'))
FROM credit_payments.gateway_payments", verify);
        await using var verifyReader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        Assert.Equal(2L, verifyReader.GetInt64(0));
        Assert.Equal(1L, verifyReader.GetInt64(1));
        Assert.Equal(0L, verifyReader.GetInt64(2));
    }

    [Fact]
    public async Task Concurrent_create_or_get_uses_one_durable_source_order()
    {
        await using (var migration = CreateOrders())
        {
            await EnsureOrdersMigrationPrerequisitesAsync(migration);
            await migration.Database.MigrateAsync();
        }

        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var command = ExternalOrderCommand(paymentId, customerId, supplierId);

        var calls = Enumerable.Range(0, 8)
            .Select(_ => RunCreateOrGetAsync(command))
            .ToArray();
        var results = await Task.WhenAll(calls);

        Assert.Single(results.Select(x => x.OrderId).Distinct());
        Assert.Equal(1, results.Count(x => x.WasCreated));

        await using var verification = CreateOrders();
        var persisted = await verification.Orders
            .Include(x => x.Items)
            .Where(x => x.ReferenceType == "CreditPayments"
                && x.SourceReferenceId == paymentId)
            .ToListAsync();
        var order = Assert.Single(persisted);
        Assert.Equal(customerId, order.UserId);
        Assert.Equal(10_000_000, order.FinalAmountMinor);
        Assert.Equal("IRR", order.Currency);
        Assert.Equal(supplierId, order.SourceOwnerId);
        Assert.Equal("store", Assert.Single(order.Items).CategoryCode);
    }

    [Fact]
    public async Task Unknown_create_outcome_is_recovered_without_a_second_order()
    {
        await using var db = CreateOrders();
        await EnsureOrdersMigrationPrerequisitesAsync(db);
        await db.Database.MigrateAsync();
        var repository = new OrderRepository(db);
        var inner = new OrderCreationGateway(
            repository,
            new UnusedMediator(),
            NullLogger<OrderCreationGateway>.Instance);
        var uncertain = new CommitThenThrowGateway(inner);
        var handler = new CreateOrGetExternalPaymentOrderCommandHandler(
            uncertain,
            repository,
            NullLogger<CreateOrGetExternalPaymentOrderCommandHandler>.Instance);
        var command = ExternalOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var recovered = await handler.Handle(command, default);
        var replay = await handler.Handle(command, default);

        Assert.Equal(recovered.OrderId, replay.OrderId);
        Assert.False(recovered.WasCreated);
        Assert.False(replay.WasCreated);
        Assert.Equal(1, uncertain.Calls);
        Assert.Equal(1, await db.Orders.CountAsync(x =>
            x.ReferenceType == "CreditPayments"
            && x.SourceReferenceId == command.SourceReferenceId));
    }

    private static Task<int> InsertHistoricalPaymentAsync(
        CreditPaymentsDbContext db,
        Guid merchantId,
        Guid terminalId,
        Guid paymentId,
        string merchantOrderId) => db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO credit_payments.gateway_payments
(gateway_payment_id, merchant_id, terminal_id, merchant_order_id, amount_minor,
 currency, metadata, status, return_url, expires_at, merchant_display_name_snapshot,
 terminal_name_snapshot, approved_category_code_snapshot, created_at, updated_at, version)
VALUES ({paymentId}, {merchantId}, {terminalId}, {merchantOrderId}, {500L}, {'I' + "RR"},
 CAST({"{}"} AS jsonb), {(short)GatewayPaymentStatus.AwaitingCustomer},
 {"https://merchant.example/callback"}, {Now.AddMinutes(15)}, {"Historical Merchant"},
 {"Historical Terminal"}, {"store"}, {Now}, {Now}, {1L})");

    private CreditPaymentsDbContext Create() => new(
        new DbContextOptionsBuilder<CreditPaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options);

    private OrdersDbContext CreateOrders() => new(
        new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options);

    private static Task EnsureOrdersMigrationPrerequisitesAsync(OrdersDbContext db) =>
        db.Database.ExecuteSqlRawAsync(
            """
            CREATE SCHEMA IF NOT EXISTS store;
            CREATE TABLE IF NOT EXISTS store.shops
            (
                "Id" uuid PRIMARY KEY,
                "SupplierId" uuid NOT NULL
            );
            """);

    private async Task<ExternalPaymentOrderDto> RunCreateOrGetAsync(
        CreateOrGetExternalPaymentOrderCommand command)
    {
        await using var db = CreateOrders();
        var repository = new OrderRepository(db);
        var gateway = new OrderCreationGateway(
            repository,
            new UnusedMediator(),
            NullLogger<OrderCreationGateway>.Instance);
        var handler = new CreateOrGetExternalPaymentOrderCommandHandler(
            gateway,
            repository,
            NullLogger<CreateOrGetExternalPaymentOrderCommandHandler>.Instance);
        return await handler.Handle(command, default);
    }

    private static CreateOrGetExternalPaymentOrderCommand ExternalOrderCommand(
        Guid paymentId,
        Guid customerId,
        Guid supplierId) => new(
            customerId,
            "CreditPayments",
            paymentId,
            supplierId,
            10_000_000,
            "IRR",
            "store",
            "پرداخت درگاه اعتباری",
            $"cpg-order-{paymentId:N}",
            Now.AddMinutes(15));

    private sealed class CommitThenThrowGateway(IOrderCreationGateway inner)
        : IOrderCreationGateway
    {
        public int Calls { get; private set; }

        public async Task<CreateOrderResponse> CreateAsync(
            CreateOrderCommand request,
            CancellationToken cancellationToken)
        {
            Calls++;
            var result = await inner.CreateAsync(request, cancellationToken);
            throw new IOException($"Unknown outcome after committed Order {result.OrderId:D}");
        }
    }

    private sealed class UnusedMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Unexpected mediator call: {request.GetType().Name}");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"Unexpected mediator call: {request.GetType().Name}");
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
