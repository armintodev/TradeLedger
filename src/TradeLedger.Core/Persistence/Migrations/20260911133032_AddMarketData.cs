using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMarketData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "backtest");

        migrationBuilder.EnsureSchema(
            name: "market");

        migrationBuilder.CreateTable(
            name: "candle_imports",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                file_bytes = table.Column<long>(type: "bigint", nullable: false),
                content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                source = table.Column<string>(type: "text", nullable: false),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                interval = table.Column<string>(type: "text", nullable: false),
                rows_parsed = table.Column<int>(type: "integer", nullable: false),
                rows_inserted = table.Column<int>(type: "integer", nullable: false),
                rows_skipped_as_duplicate = table.Column<int>(type: "integer", nullable: false),
                first_open_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_open_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                warnings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candle_imports", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "candles",
            schema: "market",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                source = table.Column<string>(type: "text", nullable: false),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                interval = table.Column<string>(type: "text", nullable: false),
                open_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                open_time_raw_ms = table.Column<long>(type: "bigint", nullable: false),
                open = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                high = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                low = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                close = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                volume = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                quote_volume = table.Column<decimal>(type: "numeric(28,12)", nullable: true),
                trade_count = table.Column<int>(type: "integer", nullable: true),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_candles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "funding_rates",
            schema: "market",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                source = table.Column<string>(type: "text", nullable: false),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                funding_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                funding_time_raw_ms = table.Column<long>(type: "bigint", nullable: false),
                funding_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_funding_rates", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "market_data_backfill_jobs",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                source = table.Column<string>(type: "text", nullable: false),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                interval = table.Column<string>(type: "text", nullable: false),
                from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                include_funding_rates = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                candles_written = table.Column<int>(type: "integer", nullable: false),
                funding_rates_written = table.Column<int>(type: "integer", nullable: false),
                progress_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                cancellation_requested = table.Column<bool>(type: "boolean", nullable: false),
                queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_market_data_backfill_jobs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "market_symbol_aliases",
            schema: "market",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                canonical_symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                source = table.Column<string>(type: "text", nullable: false),
                source_symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_market_symbol_aliases", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_candle_imports_user_id_imported_at",
            schema: "backtest",
            table: "candle_imports",
            columns: new[] { "user_id", "imported_at" });

        migrationBuilder.CreateIndex(
            name: "ix_candles_source_symbol_interval_open_time",
            schema: "market",
            table: "candles",
            columns: new[] { "source", "symbol", "interval", "open_time" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_funding_rates_source_symbol_funding_time",
            schema: "market",
            table: "funding_rates",
            columns: new[] { "source", "symbol", "funding_time" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_market_data_backfill_jobs_status_queued_at",
            schema: "backtest",
            table: "market_data_backfill_jobs",
            columns: new[] { "status", "queued_at" });

        migrationBuilder.CreateIndex(
            name: "ix_market_data_backfill_jobs_user_id_queued_at",
            schema: "backtest",
            table: "market_data_backfill_jobs",
            columns: new[] { "user_id", "queued_at" });

        migrationBuilder.CreateIndex(
            name: "ix_market_symbol_aliases_canonical_symbol_source",
            schema: "market",
            table: "market_symbol_aliases",
            columns: new[] { "canonical_symbol", "source" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "candle_imports",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "candles",
            schema: "market");

        migrationBuilder.DropTable(
            name: "funding_rates",
            schema: "market");

        migrationBuilder.DropTable(
            name: "market_data_backfill_jobs",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "market_symbol_aliases",
            schema: "market");
    }
}
