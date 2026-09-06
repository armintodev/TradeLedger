using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                starting_balance = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                journal_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                default_risk_per_trade = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "raw_exchange_payloads",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                external_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_raw_exchange_payloads", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "taxonomy_terms",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                color_hex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_taxonomy_terms", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "AspNetRoles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_user_claims_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "AspNetUsers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_asp_net_user_logins_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "AspNetUsers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "AspNetRoles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "AspNetUsers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "AspNetUsers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                venue = table.Column<string>(type: "text", nullable: false),
                sync_mode = table.Column<string>(type: "text", nullable: false),
                quote_asset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                tracked_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", x => x.id);
                table.ForeignKey(
                    name: "fk_accounts_users_user_id",
                    column: x => x.user_id,
                    principalTable: "AspNetUsers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "balance_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                asset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                wallet_balance = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                available = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                frozen = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                margin = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                unrealized_pnl = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                equity = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                bonus = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                is_manual = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_balance_snapshots", x => x.id);
                table.ForeignKey(
                    name: "fk_balance_snapshots_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exchange_credentials",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                venue = table.Column<string>(type: "text", nullable: false),
                api_key_cipher = table.Column<byte[]>(type: "bytea", nullable: false),
                api_secret_cipher = table.Column<byte[]>(type: "bytea", nullable: false),
                api_key_hint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_verification_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exchange_credentials", x => x.id);
                table.ForeignKey(
                    name: "fk_exchange_credentials_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "holdings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                asset = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                average_entry_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                entry_value_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                current_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                current_value_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                priced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                pool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                farm_apr = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                is_farmed = table.Column<bool>(type: "boolean", nullable: false),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_holdings", x => x.id);
                table.ForeignKey(
                    name: "fk_holdings_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "sync_cursors",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                last_record_ms = table.Column<long>(type: "bigint", nullable: true),
                last_record_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                backfill_complete = table.Column<bool>(type: "boolean", nullable: false),
                backfill_cursor_ms = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sync_cursors", x => x.id);
                table.ForeignKey(
                    name: "fk_sync_cursors_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "sync_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                is_backfill = table.Column<bool>(type: "boolean", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                records_seen = table.Column<int>(type: "integer", nullable: false),
                records_written = table.Column<int>(type: "integer", nullable: false),
                requests_made = table.Column<int>(type: "integer", nullable: false),
                error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sync_runs", x => x.id);
                table.ForeignKey(
                    name: "fk_sync_runs_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "trade_plans",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: true),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                side = table.Column<string>(type: "text", nullable: false),
                strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                timeframe_id = table.Column<Guid>(type: "uuid", nullable: true),
                entry_mental_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                planned_entry_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                planned_stop_loss_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                planned_take_profit_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                risk_fraction = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                planned_risk_reward = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                leverage = table.Column<int>(type: "integer", nullable: false),
                average_fee_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                planned_quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: true),
                planned_order_value = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                planned_margin = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                estimated_profit = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                estimated_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                balance_at_planning = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                ctx_total2 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_btc_dominance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_usdt_dominance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_market_trend = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_sma = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_market_session = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_btc_pair = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_rsi = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_volume = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_candle_shape = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                status = table.Column<string>(type: "text", nullable: false),
                notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                linked_trade_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_trade_plans", x => x.id);
                table.ForeignKey(
                    name: "fk_trade_plans_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trade_plans_taxonomy_terms_entry_mental_state_id",
                    column: x => x.entry_mental_state_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trade_plans_taxonomy_terms_strategy_id",
                    column: x => x.strategy_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trade_plans_taxonomy_terms_timeframe_id",
                    column: x => x.timeframe_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "transfers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                to_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                direction = table.Column<string>(type: "text", nullable: false),
                asset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                amount = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                fee = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                value_usd = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                write_off = table.Column<bool>(type: "boolean", nullable: false),
                network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                tx_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                counterparty = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_transfers", x => x.id);
                table.ForeignKey(
                    name: "fk_transfers_accounts_from_account_id",
                    column: x => x.from_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_transfers_accounts_to_account_id",
                    column: x => x.to_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "trades",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                origin = table.Column<string>(type: "text", nullable: false),
                review_state = table.Column<string>(type: "text", nullable: false),
                exchange_position_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                side = table.Column<string>(type: "text", nullable: false),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                opened_at_raw_ms = table.Column<long>(type: "bigint", nullable: true),
                entry_price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                position_margin = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                leverage = table.Column<int>(type: "integer", nullable: false),
                order_value = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                margin_mode = table.Column<string>(type: "text", nullable: true),
                position_mode = table.Column<string>(type: "text", nullable: true),
                order_type = table.Column<string>(type: "text", nullable: false),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                closed_at_raw_ms = table.Column<long>(type: "bigint", nullable: true),
                exit_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                percent_closed = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                liquidated_quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: true),
                liquidation_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                stop_loss_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                take_profit_price = table.Column<decimal>(type: "numeric(28,18)", nullable: true),
                position_to_account_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                planned_stop_loss_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                account_risked_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                planned_return_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                gross_profit_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                fees = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                funding = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                net_profit_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                achieved_return_r = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                trade_gain_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                account_change_percent = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                balance_after = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                outcome = table.Column<string>(type: "text", nullable: false),
                strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                timeframe_id = table.Column<Guid>(type: "uuid", nullable: true),
                entry_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                exit_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                entry_mental_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                exit_mental_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                ctx_total2 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_btc_dominance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_usdt_dominance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_market_trend = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_sma = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_market_session = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_btc_pair = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_rsi = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_volume = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ctx_candle_shape = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                rating = table.Column<int>(type: "integer", nullable: true),
                memo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                tag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                post_trade_tag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                trade_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_planned = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_trades", x => x.id);
                table.ForeignKey(
                    name: "fk_trades_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_entry_mental_state_id",
                    column: x => x.entry_mental_state_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_entry_type_id",
                    column: x => x.entry_type_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_exit_mental_state_id",
                    column: x => x.exit_mental_state_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_exit_type_id",
                    column: x => x.exit_type_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_strategy_id",
                    column: x => x.strategy_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_taxonomy_terms_timeframe_id",
                    column: x => x.timeframe_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_trades_trade_plans_trade_plan_id",
                    column: x => x.trade_plan_id,
                    principalTable: "trade_plans",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "attachments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                trade_id = table.Column<Guid>(type: "uuid", nullable: true),
                transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                slot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attachments", x => x.id);
                table.ForeignKey(
                    name: "fk_attachments_trades_trade_id",
                    column: x => x.trade_id,
                    principalTable: "trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_attachments_transfers_transfer_id",
                    column: x => x.transfer_id,
                    principalTable: "transfers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "executions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                trade_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "text", nullable: false),
                side = table.Column<string>(type: "text", nullable: false),
                price = table.Column<decimal>(type: "numeric(28,18)", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                fee = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                fee_asset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                realized_profit_loss = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                executed_at_raw_ms = table.Column<long>(type: "bigint", nullable: true),
                exchange_trade_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                exchange_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                order_type = table.Column<string>(type: "text", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_executions", x => x.id);
                table.ForeignKey(
                    name: "fk_executions_trades_trade_id",
                    column: x => x.trade_id,
                    principalTable: "trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "funding_payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                trade_id = table.Column<Guid>(type: "uuid", nullable: true),
                symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                amount = table.Column<decimal>(type: "numeric(28,8)", nullable: false),
                asset = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                funding_rate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                occurred_at_raw_ms = table.Column<long>(type: "bigint", nullable: true),
                exchange_funding_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_funding_payments", x => x.id);
                table.ForeignKey(
                    name: "fk_funding_payments_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_funding_payments_trades_trade_id",
                    column: x => x.trade_id,
                    principalTable: "trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "trade_mistakes",
            columns: table => new
            {
                trade_id = table.Column<Guid>(type: "uuid", nullable: false),
                taxonomy_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                estimated_cost = table.Column<decimal>(type: "numeric(28,8)", nullable: true),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_trade_mistakes", x => new { x.trade_id, x.taxonomy_term_id });
                table.ForeignKey(
                    name: "fk_trade_mistakes_taxonomy_terms_taxonomy_term_id",
                    column: x => x.taxonomy_term_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_trade_mistakes_trades_trade_id",
                    column: x => x.trade_id,
                    principalTable: "trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "trade_trackings",
            columns: table => new
            {
                trade_id = table.Column<Guid>(type: "uuid", nullable: false),
                taxonomy_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_trade_trackings", x => new { x.trade_id, x.taxonomy_term_id });
                table.ForeignKey(
                    name: "fk_trade_trackings_taxonomy_terms_taxonomy_term_id",
                    column: x => x.taxonomy_term_id,
                    principalTable: "taxonomy_terms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_trade_trackings_trades_trade_id",
                    column: x => x.trade_id,
                    principalTable: "trades",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_role_claims_role_id",
            table: "AspNetRoleClaims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_claims_user_id",
            table: "AspNetUserClaims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_logins_user_id",
            table: "AspNetUserLogins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_roles_role_id",
            table: "AspNetUserRoles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_accounts_user_id_name",
            table: "accounts",
            columns: new[] { "user_id", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_attachments_trade_id",
            table: "attachments",
            column: "trade_id");

        migrationBuilder.CreateIndex(
            name: "ix_attachments_transfer_id",
            table: "attachments",
            column: "transfer_id");

        migrationBuilder.CreateIndex(
            name: "ix_balance_snapshots_account_id_captured_at",
            table: "balance_snapshots",
            columns: new[] { "account_id", "captured_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_balance_snapshots_user_id_captured_at",
            table: "balance_snapshots",
            columns: new[] { "user_id", "captured_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_exchange_credentials_account_id",
            table: "exchange_credentials",
            column: "account_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_executions_account_id_exchange_trade_id",
            table: "executions",
            columns: new[] { "account_id", "exchange_trade_id" },
            unique: true,
            filter: "exchange_trade_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_executions_trade_id_executed_at",
            table: "executions",
            columns: new[] { "trade_id", "executed_at" });

        migrationBuilder.CreateIndex(
            name: "ix_funding_payments_account_id_exchange_funding_id",
            table: "funding_payments",
            columns: new[] { "account_id", "exchange_funding_id" },
            unique: true,
            filter: "exchange_funding_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_funding_payments_trade_id",
            table: "funding_payments",
            column: "trade_id");

        migrationBuilder.CreateIndex(
            name: "ix_holdings_account_id",
            table: "holdings",
            column: "account_id");

        migrationBuilder.CreateIndex(
            name: "ix_holdings_user_id_kind_asset",
            table: "holdings",
            columns: new[] { "user_id", "kind", "asset" });

        migrationBuilder.CreateIndex(
            name: "ix_raw_exchange_payloads_account_id_endpoint_external_id",
            table: "raw_exchange_payloads",
            columns: new[] { "account_id", "endpoint", "external_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_sync_cursors_account_id_endpoint",
            table: "sync_cursors",
            columns: new[] { "account_id", "endpoint" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_sync_runs_account_id_started_at",
            table: "sync_runs",
            columns: new[] { "account_id", "started_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_taxonomy_terms_user_id_kind_name",
            table: "taxonomy_terms",
            columns: new[] { "user_id", "kind", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_trade_mistakes_taxonomy_term_id",
            table: "trade_mistakes",
            column: "taxonomy_term_id");

        migrationBuilder.CreateIndex(
            name: "ix_trade_plans_account_id",
            table: "trade_plans",
            column: "account_id");

        migrationBuilder.CreateIndex(
            name: "ix_trade_plans_entry_mental_state_id",
            table: "trade_plans",
            column: "entry_mental_state_id");

        migrationBuilder.CreateIndex(
            name: "ix_trade_plans_strategy_id",
            table: "trade_plans",
            column: "strategy_id");

        migrationBuilder.CreateIndex(
            name: "ix_trade_plans_timeframe_id",
            table: "trade_plans",
            column: "timeframe_id");

        migrationBuilder.CreateIndex(
            name: "ix_trade_plans_user_id_status_symbol",
            table: "trade_plans",
            columns: new[] { "user_id", "status", "symbol" });

        migrationBuilder.CreateIndex(
            name: "ix_trade_trackings_taxonomy_term_id",
            table: "trade_trackings",
            column: "taxonomy_term_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_account_id_exchange_position_id",
            table: "trades",
            columns: new[] { "account_id", "exchange_position_id" },
            unique: true,
            filter: "exchange_position_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_trades_entry_mental_state_id",
            table: "trades",
            column: "entry_mental_state_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_entry_type_id",
            table: "trades",
            column: "entry_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_exit_mental_state_id",
            table: "trades",
            column: "exit_mental_state_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_exit_type_id",
            table: "trades",
            column: "exit_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_strategy_id",
            table: "trades",
            column: "strategy_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_timeframe_id",
            table: "trades",
            column: "timeframe_id");

        migrationBuilder.CreateIndex(
            name: "ix_trades_trade_plan_id",
            table: "trades",
            column: "trade_plan_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_trades_user_id_opened_at",
            table: "trades",
            columns: new[] { "user_id", "opened_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_trades_user_id_review_state",
            table: "trades",
            columns: new[] { "user_id", "review_state" });

        migrationBuilder.CreateIndex(
            name: "ix_trades_user_id_strategy_id",
            table: "trades",
            columns: new[] { "user_id", "strategy_id" });

        migrationBuilder.CreateIndex(
            name: "ix_trades_user_id_symbol",
            table: "trades",
            columns: new[] { "user_id", "symbol" });

        migrationBuilder.CreateIndex(
            name: "ix_transfers_from_account_id",
            table: "transfers",
            column: "from_account_id");

        migrationBuilder.CreateIndex(
            name: "ix_transfers_to_account_id",
            table: "transfers",
            column: "to_account_id");

        migrationBuilder.CreateIndex(
            name: "ix_transfers_user_id_occurred_at",
            table: "transfers",
            columns: new[] { "user_id", "occurred_at" },
            descending: new[] { false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "attachments");

        migrationBuilder.DropTable(
            name: "balance_snapshots");

        migrationBuilder.DropTable(
            name: "exchange_credentials");

        migrationBuilder.DropTable(
            name: "executions");

        migrationBuilder.DropTable(
            name: "funding_payments");

        migrationBuilder.DropTable(
            name: "holdings");

        migrationBuilder.DropTable(
            name: "raw_exchange_payloads");

        migrationBuilder.DropTable(
            name: "sync_cursors");

        migrationBuilder.DropTable(
            name: "sync_runs");

        migrationBuilder.DropTable(
            name: "trade_mistakes");

        migrationBuilder.DropTable(
            name: "trade_trackings");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "transfers");

        migrationBuilder.DropTable(
            name: "trades");

        migrationBuilder.DropTable(
            name: "trade_plans");

        migrationBuilder.DropTable(
            name: "accounts");

        migrationBuilder.DropTable(
            name: "taxonomy_terms");

        migrationBuilder.DropTable(
            name: "AspNetUsers");
    }
}
