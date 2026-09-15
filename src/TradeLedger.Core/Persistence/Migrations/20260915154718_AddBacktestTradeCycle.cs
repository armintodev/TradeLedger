using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBacktestTradeCycle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "cycle_adx",
            schema: "backtest",
            table: "backtest_trades",
            type: "numeric(18,6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "cycle_interval",
            schema: "backtest",
            table: "backtest_trades",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "cycle_adx",
            schema: "backtest",
            table: "backtest_trades");

        migrationBuilder.DropColumn(
            name: "cycle_interval",
            schema: "backtest",
            table: "backtest_trades");
    }
}
