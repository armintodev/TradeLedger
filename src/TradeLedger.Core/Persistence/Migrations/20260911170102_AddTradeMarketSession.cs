using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTradeMarketSession : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "market_session",
            schema: "core",
            table: "trades",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.Sql(
            """
            UPDATE core.trades
            SET market_session = CASE
                WHEN EXTRACT(HOUR FROM opened_at AT TIME ZONE 'UTC') <  7 THEN 'Tokyo'
                WHEN EXTRACT(HOUR FROM opened_at AT TIME ZONE 'UTC') <  9 THEN 'Tokyo, London'
                WHEN EXTRACT(HOUR FROM opened_at AT TIME ZONE 'UTC') < 12 THEN 'London'
                WHEN EXTRACT(HOUR FROM opened_at AT TIME ZONE 'UTC') < 16 THEN 'London, NewYork'
                WHEN EXTRACT(HOUR FROM opened_at AT TIME ZONE 'UTC') < 21 THEN 'NewYork'
                ELSE 'None'
            END;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_trades_user_id_market_session",
            schema: "core",
            table: "trades",
            columns: new[] { "user_id", "market_session" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_trades_user_id_market_session",
            schema: "core",
            table: "trades");

        migrationBuilder.DropColumn(
            name: "market_session",
            schema: "core",
            table: "trades");
    }
}
