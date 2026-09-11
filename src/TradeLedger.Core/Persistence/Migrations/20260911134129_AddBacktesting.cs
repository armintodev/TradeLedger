using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBacktesting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "backtest_strategies",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                rule_json = table.Column<string>(type: "jsonb", nullable: false),
                rule_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                strategy_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_strategies", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_strategies_taxonomy_terms_strategy_term_id",
                    column: x => x.strategy_term_id,
                    principalSchema: "core",
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "backtest_accounts",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                starting_balance = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                mode = table.Column<string>(type: "text", nullable: false),
                backtest_strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_accounts", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_accounts_backtest_strategies_backtest_strategy_id",
                    column: x => x.backtest_strategy_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_strategies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "backtest_runs",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                backtest_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                backtest_strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                rule_json = table.Column<string>(type: "jsonb", nullable: true),
                rule_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                source = table.Column<string>(type: "text", nullable: true),
                interval = table.Column<string>(type: "text", nullable: true),
                from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                opening_balance = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                closing_balance = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                risk_percent_per_position = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                risk_reward_ratio = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                leverage = table.Column<int>(type: "integer", nullable: false),
                taker_fee_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                maker_fee_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                slippage_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                maintenance_margin_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                include_funding = table.Column<bool>(type: "boolean", nullable: false),
                parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                what_if_json = table.Column<string>(type: "jsonb", nullable: true),
                allow_gaps = table.Column<bool>(type: "boolean", nullable: false),
                data_quality = table.Column<string>(type: "text", nullable: false),
                engine_version = table.Column<int>(type: "integer", nullable: false),
                total_bars = table.Column<int>(type: "integer", nullable: false),
                bars_processed = table.Column<int>(type: "integer", nullable: false),
                progress_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                cancellation_requested = table.Column<bool>(type: "boolean", nullable: false),
                queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                result_json = table.Column<string>(type: "jsonb", nullable: true),
                warnings_json = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_runs", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_runs_backtest_accounts_backtest_account_id",
                    column: x => x.backtest_account_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_backtest_runs_backtest_strategies_backtest_strategy_id",
                    column: x => x.backtest_strategy_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_strategies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "backtest_equity_points",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                backtest_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                equity = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                drawdown = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                drawdown_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_equity_points", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_equity_points_backtest_runs_backtest_run_id",
                    column: x => x.backtest_run_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "backtest_trades",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                backtest_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                side = table.Column<string>(type: "text", nullable: false),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                entry_bar_index = table.Column<int>(type: "integer", nullable: false),
                exit_bar_index = table.Column<int>(type: "integer", nullable: false),
                bars_in_trade = table.Column<int>(type: "integer", nullable: false),
                entry_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                exit_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                leverage = table.Column<int>(type: "integer", nullable: false),
                position_margin = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                order_value = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                stop_loss_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                take_profit_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                liquidation_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                gross_profit_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                fees = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                funding = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                net_profit_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                achieved_return_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                planned_return_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                trade_gain_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                balance_after = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                outcome = table.Column<string>(type: "text", nullable: false),
                exit_reason = table.Column<string>(type: "text", nullable: true),
                intrabar_resolution = table.Column<string>(type: "text", nullable: false),
                was_liquidated = table.Column<bool>(type: "boolean", nullable: false),
                mae_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                mfe_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                source_trade_id = table.Column<Guid>(type: "uuid", nullable: true),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_trades", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_trades_backtest_runs_backtest_run_id",
                    column: x => x.backtest_run_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "backtest_executions",
            schema: "backtest",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                backtest_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                backtest_trade_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "text", nullable: false),
                price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                fee = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                bar_index = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backtest_executions", x => x.id);
                table.ForeignKey(
                    name: "fk_backtest_executions_backtest_trades_backtest_trade_id",
                    column: x => x.backtest_trade_id,
                    principalSchema: "backtest",
                    principalTable: "backtest_trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_accounts_backtest_strategy_id",
            schema: "backtest",
            table: "backtest_accounts",
            column: "backtest_strategy_id");

        migrationBuilder.CreateIndex(
            name: "ix_backtest_accounts_user_id_created_at",
            schema: "backtest",
            table: "backtest_accounts",
            columns: new[] { "user_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_accounts_user_id_name",
            schema: "backtest",
            table: "backtest_accounts",
            columns: new[] { "user_id", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_backtest_equity_points_backtest_run_id_sequence",
            schema: "backtest",
            table: "backtest_equity_points",
            columns: new[] { "backtest_run_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_backtest_executions_backtest_trade_id_bar_index",
            schema: "backtest",
            table: "backtest_executions",
            columns: new[] { "backtest_trade_id", "bar_index" });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_runs_backtest_account_id_from",
            schema: "backtest",
            table: "backtest_runs",
            columns: new[] { "backtest_account_id", "from" });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_runs_backtest_strategy_id",
            schema: "backtest",
            table: "backtest_runs",
            column: "backtest_strategy_id");

        migrationBuilder.CreateIndex(
            name: "ix_backtest_runs_status_queued_at",
            schema: "backtest",
            table: "backtest_runs",
            columns: new[] { "status", "queued_at" });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_runs_user_id_queued_at",
            schema: "backtest",
            table: "backtest_runs",
            columns: new[] { "user_id", "queued_at" });

        migrationBuilder.CreateIndex(
            name: "ix_backtest_strategies_strategy_term_id",
            schema: "backtest",
            table: "backtest_strategies",
            column: "strategy_term_id");

        migrationBuilder.CreateIndex(
            name: "ix_backtest_strategies_user_id_name",
            schema: "backtest",
            table: "backtest_strategies",
            columns: new[] { "user_id", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_backtest_trades_backtest_run_id_sequence",
            schema: "backtest",
            table: "backtest_trades",
            columns: new[] { "backtest_run_id", "sequence" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "backtest_equity_points",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "backtest_executions",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "backtest_trades",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "backtest_runs",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "backtest_accounts",
            schema: "backtest");

        migrationBuilder.DropTable(
            name: "backtest_strategies",
            schema: "backtest");
    }
}
