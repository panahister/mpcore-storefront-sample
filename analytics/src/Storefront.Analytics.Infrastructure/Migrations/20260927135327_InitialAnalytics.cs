using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MPCore.Persistence.Timescale;

#nullable disable

namespace Storefront.Analytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.EnsureSchema(
                name: "idempotency");

            migrationBuilder.CreateTable(
                name: "order_facts",
                schema: "analytics",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    city = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    recorded_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_facts", x => new { x.event_id, x.occurred_on_utc });
                });

            migrationBuilder.CreateTable(
                name: "processed_messages",
                schema: "idempotency",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProcessedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_messages", x => new { x.Consumer, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "idempotency",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Operation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Response = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requests", x => new { x.Scope, x.Key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_facts_kind_time",
                schema: "analytics",
                table: "order_facts",
                columns: new[] { "kind", "occurred_on_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_ProcessedOnUtc",
                schema: "idempotency",
                table: "processed_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_requests_CreatedOnUtc",
                schema: "idempotency",
                table: "requests",
                column: "CreatedOnUtc");

            // The table of facts is a time series. TimescaleDB partitions it into one chunk per day on the
            // time of the event, so a report reads the chunks its period touches and nothing else. Written
            // by hand: Entity Framework does not know what a hypertable is.
            migrationBuilder.EnsureTimescale();
            migrationBuilder.CreateHypertable("order_facts", "occurred_on_utc", schema: "analytics", chunkInterval: "1 day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_facts",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "processed_messages",
                schema: "idempotency");

            migrationBuilder.DropTable(
                name: "requests",
                schema: "idempotency");
        }
    }
}
