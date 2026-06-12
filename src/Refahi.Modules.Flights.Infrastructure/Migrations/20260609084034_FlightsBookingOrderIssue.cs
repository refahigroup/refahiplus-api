using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlightsBookingOrderIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "flights");

            migrationBuilder.CreateTable(
                name: "flight_bookings",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    provider_booking_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_booking_caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    provider_pnr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_booking_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_booking_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    provider_booked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    contact_mobile_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    base_fare_amount = table.Column<long>(type: "bigint", nullable: false),
                    base_fare_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    taxes_amount = table.Column<long>(type: "bigint", nullable: false),
                    taxes_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    fees_amount = table.Column<long>(type: "bigint", nullable: false),
                    fees_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    discount_amount = table.Column<long>(type: "bigint", nullable: false),
                    discount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payable_amount_amount = table.Column<long>(type: "bigint", nullable: false),
                    payable_amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issue_failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    latest_cancellation_quote_penalty_amount = table.Column<long>(type: "bigint", nullable: true),
                    latest_cancellation_quote_penalty_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    latest_cancellation_quote_refund_amount = table.Column<long>(type: "bigint", nullable: true),
                    latest_cancellation_quote_refund_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    latest_cancellation_quote_quoted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    latest_cancellation_quote_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    latest_cancellation_quote_provider_quote_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latest_cancellation_quote_provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latest_cancellation_quote_snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_bookings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flight_search_offer_snapshots",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_token = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_fare_source_code = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    provider_search_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_fare_amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    public_offer_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    provider_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_search_offer_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flight_booking_passengers",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    last_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    national_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    passport_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nationality_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    flight_booking_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_booking_passengers", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_booking_passengers_flight_bookings_flight_booking_id",
                        column: x => x.flight_booking_id,
                        principalSchema: "flights",
                        principalTable: "flight_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_booking_segments",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    provider_segment_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    flight_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    airline_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    airline_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin_airport_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    origin_caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_airport_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    destination_caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    departure_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    arrival_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    flight_booking_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_booking_segments", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_booking_segments_flight_bookings_flight_booking_id",
                        column: x => x.flight_booking_id,
                        principalSchema: "flights",
                        principalTable: "flight_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_booking_tickets",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    passenger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    passenger_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    provider_ticket_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ticket_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    flight_booking_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_booking_tickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_booking_tickets_flight_bookings_flight_booking_id",
                        column: x => x.flight_booking_id,
                        principalSchema: "flights",
                        principalTable: "flight_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_cancellation_requests",
                schema: "flights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_penalty_amount = table.Column<long>(type: "bigint", nullable: false),
                    quote_penalty_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    quote_refund_amount = table.Column<long>(type: "bigint", nullable: false),
                    quote_refund_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    quote_quoted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quote_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    quote_provider_quote_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quote_provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quote_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    provider_cancellation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    flight_booking_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_cancellation_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_cancellation_requests_flight_bookings_flight_booking~",
                        column: x => x.flight_booking_id,
                        principalSchema: "flights",
                        principalTable: "flight_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_offer_snapshots",
                schema: "flights",
                columns: table => new
                {
                    flight_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_fare_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fare_caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cabin_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    booking_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fare_rules_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    provider_trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_offer_snapshots", x => x.flight_booking_id);
                    table.ForeignKey(
                        name: "FK_flight_offer_snapshots_flight_bookings_flight_booking_id",
                        column: x => x.flight_booking_id,
                        principalSchema: "flights",
                        principalTable: "flight_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flight_booking_passengers_booking_id",
                schema: "flights",
                table: "flight_booking_passengers",
                column: "flight_booking_id");

            migrationBuilder.CreateIndex(
                name: "ux_flight_booking_segments_booking_sequence",
                schema: "flights",
                table: "flight_booking_segments",
                columns: new[] { "flight_booking_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flight_booking_tickets_booking_id",
                schema: "flights",
                table: "flight_booking_tickets",
                column: "flight_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_flight_booking_tickets_ticket_number",
                schema: "flights",
                table: "flight_booking_tickets",
                column: "ticket_number");

            migrationBuilder.CreateIndex(
                name: "ix_flight_bookings_status",
                schema: "flights",
                table: "flight_bookings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_flight_bookings_user_id",
                schema: "flights",
                table: "flight_bookings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_flight_bookings_idempotency_key",
                schema: "flights",
                table: "flight_bookings",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_flight_bookings_order_id",
                schema: "flights",
                table: "flight_bookings",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flight_cancellation_requests_booking_id",
                schema: "flights",
                table: "flight_cancellation_requests",
                column: "flight_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_flight_search_offer_snapshots_expires_at_utc",
                schema: "flights",
                table: "flight_search_offer_snapshots",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_flight_search_offer_snapshots_offer_token",
                schema: "flights",
                table: "flight_search_offer_snapshots",
                column: "offer_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flight_booking_passengers",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_booking_segments",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_booking_tickets",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_cancellation_requests",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_offer_snapshots",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_search_offer_snapshots",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "flight_bookings",
                schema: "flights");
        }
    }
}
