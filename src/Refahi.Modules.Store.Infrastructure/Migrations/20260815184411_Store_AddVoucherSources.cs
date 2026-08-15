using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_AddVoucherSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vouchers_CodeHash",
                schema: "store",
                table: "vouchers");

            migrationBuilder.AddColumn<short>(
                name: "RedemptionMode",
                schema: "store",
                table: "vouchers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<short>(
                name: "SourceType",
                schema: "store",
                table: "vouchers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherSourceCodeId",
                schema: "store",
                table: "vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherSourceId",
                schema: "store",
                table: "vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherSourceTitle",
                schema: "store",
                table: "vouchers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayableUntilUtc",
                schema: "store",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherDefaultValidityDays",
                schema: "store",
                table: "store_order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "VoucherRedemptionMode",
                schema: "store",
                table: "store_order_items",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherSourceId",
                schema: "store",
                table: "store_order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherSourceTitle",
                schema: "store",
                table: "store_order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "VoucherSourceType",
                schema: "store",
                table: "store_order_items",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherSourceId",
                schema: "store",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherSourceId",
                schema: "store",
                table: "product_variants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "voucher_deliveries",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_deliveries_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalSchema: "store",
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_sources",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<short>(type: "smallint", nullable: false),
                    RedemptionMode = table.Column<short>(type: "smallint", nullable: false),
                    DefaultValidityDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_sources", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO store.voucher_sources
                    ("Id", "SupplierId", "Title", "SourceType", "RedemptionMode",
                     "DefaultValidityDays", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT md5(s."SupplierId"::text || ':default-voucher-source')::uuid,
                       s."SupplierId", 'منبع پیش‌فرض تولید خودکار', 1, 1,
                       NULL, TRUE, NOW(), NOW()
                FROM (
                    SELECT p."SupplierId" FROM store.products p WHERE p."FulfillmentMethod" = 3
                    UNION
                    SELECT v."SupplierId" FROM store.vouchers v
                ) s;

                UPDATE store.products p
                SET "VoucherSourceId" = md5(p."SupplierId"::text || ':default-voucher-source')::uuid
                WHERE p."FulfillmentMethod" = 3 AND p."VoucherSourceId" IS NULL;

                UPDATE store.vouchers v
                SET "SourceType" = 1,
                    "RedemptionMode" = 1,
                    "VoucherSourceTitle" = 'منبع پیش‌فرض تولید خودکار',
                    "VoucherSourceId" = md5(v."SupplierId"::text || ':default-voucher-source')::uuid
                WHERE v."VoucherSourceId" IS NULL;

                UPDATE store.store_order_items i
                SET "VoucherSourceId" = p."VoucherSourceId",
                    "VoucherSourceTitle" = 'منبع پیش‌فرض تولید خودکار',
                    "VoucherSourceType" = 1,
                    "VoucherRedemptionMode" = 1
                FROM store.products p
                WHERE i."ProductId" = p."Id"
                  AND i."FulfillmentMethod" = 3
                  AND i."VoucherSourceId" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "voucher_code_import_batches",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_code_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_code_import_batches_voucher_sources_VoucherSourceId",
                        column: x => x.VoucherSourceId,
                        principalSchema: "store",
                        principalTable: "voucher_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_source_codes",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_source_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_source_codes_voucher_sources_VoucherSourceId",
                        column: x => x.VoucherSourceId,
                        principalSchema: "store",
                        principalTable: "voucher_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_code_allocations",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherSourceCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ReservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReservedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_code_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_code_allocations_store_order_items_StoreOrderItemId",
                        column: x => x.StoreOrderItemId,
                        principalSchema: "store",
                        principalTable: "store_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_code_allocations_store_orders_StoreOrderId",
                        column: x => x.StoreOrderId,
                        principalSchema: "store",
                        principalTable: "store_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_code_allocations_voucher_source_codes_VoucherSource~",
                        column: x => x.VoucherSourceCodeId,
                        principalSchema: "store",
                        principalTable: "voucher_source_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_code_allocations_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalSchema: "store",
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_SupplierId_CodeHash",
                schema: "store",
                table: "vouchers",
                columns: new[] { "SupplierId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_VoucherSourceCodeId",
                schema: "store",
                table: "vouchers",
                column: "VoucherSourceCodeId",
                unique: true,
                filter: "\"VoucherSourceCodeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_VoucherSourceId",
                schema: "store",
                table: "vouchers",
                column: "VoucherSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_items_VoucherSourceId",
                schema: "store",
                table: "store_order_items",
                column: "VoucherSourceId",
                filter: "\"VoucherSourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_VoucherSourceId",
                schema: "store",
                table: "products",
                column: "VoucherSourceId",
                filter: "\"VoucherSourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_VoucherSourceId",
                schema: "store",
                table: "product_variants",
                column: "VoucherSourceId",
                filter: "\"VoucherSourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_allocations_Status_ReservedUntilUtc",
                schema: "store",
                table: "voucher_code_allocations",
                columns: new[] { "Status", "ReservedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_allocations_StoreOrderId",
                schema: "store",
                table: "voucher_code_allocations",
                column: "StoreOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_allocations_StoreOrderItemId_SequenceNumber",
                schema: "store",
                table: "voucher_code_allocations",
                columns: new[] { "StoreOrderItemId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_allocations_VoucherId",
                schema: "store",
                table: "voucher_code_allocations",
                column: "VoucherId",
                unique: true,
                filter: "\"VoucherId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_allocations_VoucherSourceCodeId",
                schema: "store",
                table: "voucher_code_allocations",
                column: "VoucherSourceCodeId",
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_code_import_batches_VoucherSourceId_IdempotencyKey",
                schema: "store",
                table: "voucher_code_import_batches",
                columns: new[] { "VoucherSourceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_deliveries_Status_NextAttemptAtUtc",
                schema: "store",
                table: "voucher_deliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_deliveries_VoucherId_Channel",
                schema: "store",
                table: "voucher_deliveries",
                columns: new[] { "VoucherId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_source_codes_SupplierId_CodeHash",
                schema: "store",
                table: "voucher_source_codes",
                columns: new[] { "SupplierId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_source_codes_VoucherSourceId_Status_ExpiresAtUtc_Re~",
                schema: "store",
                table: "voucher_source_codes",
                columns: new[] { "VoucherSourceId", "Status", "ExpiresAtUtc", "RegisteredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_sources_SupplierId_IsActive",
                schema: "store",
                table: "voucher_sources",
                columns: new[] { "SupplierId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_sources_SupplierId_Title",
                schema: "store",
                table: "voucher_sources",
                columns: new[] { "SupplierId", "Title" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variants_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "product_variants",
                column: "VoucherSourceId",
                principalSchema: "store",
                principalTable: "voucher_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "products",
                column: "VoucherSourceId",
                principalSchema: "store",
                principalTable: "voucher_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vouchers_voucher_source_codes_VoucherSourceCodeId",
                schema: "store",
                table: "vouchers",
                column: "VoucherSourceCodeId",
                principalSchema: "store",
                principalTable: "voucher_source_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vouchers_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "vouchers",
                column: "VoucherSourceId",
                principalSchema: "store",
                principalTable: "voucher_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_variants_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_products_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_vouchers_voucher_source_codes_VoucherSourceCodeId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_vouchers_voucher_sources_VoucherSourceId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropTable(
                name: "voucher_code_allocations",
                schema: "store");

            migrationBuilder.DropTable(
                name: "voucher_code_import_batches",
                schema: "store");

            migrationBuilder.DropTable(
                name: "voucher_deliveries",
                schema: "store");

            migrationBuilder.DropTable(
                name: "voucher_source_codes",
                schema: "store");

            migrationBuilder.DropTable(
                name: "voucher_sources",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_vouchers_SupplierId_CodeHash",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropIndex(
                name: "IX_vouchers_VoucherSourceCodeId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropIndex(
                name: "IX_vouchers_VoucherSourceId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropIndex(
                name: "IX_store_order_items_VoucherSourceId",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropIndex(
                name: "IX_products_VoucherSourceId",
                schema: "store",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_variants_VoucherSourceId",
                schema: "store",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "RedemptionMode",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "VoucherSourceCodeId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "VoucherSourceId",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "VoucherSourceTitle",
                schema: "store",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "PayableUntilUtc",
                schema: "store",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "VoucherDefaultValidityDays",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropColumn(
                name: "VoucherRedemptionMode",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropColumn(
                name: "VoucherSourceId",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropColumn(
                name: "VoucherSourceTitle",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropColumn(
                name: "VoucherSourceType",
                schema: "store",
                table: "store_order_items");

            migrationBuilder.DropColumn(
                name: "VoucherSourceId",
                schema: "store",
                table: "products");

            migrationBuilder.DropColumn(
                name: "VoucherSourceId",
                schema: "store",
                table: "product_variants");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_CodeHash",
                schema: "store",
                table: "vouchers",
                column: "CodeHash",
                unique: true);
        }
    }
}
