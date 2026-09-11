using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserEgressProxy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "proxy_enabled",
            schema: "identities",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "proxy_host",
            schema: "identities",
            table: "users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "proxy_password_cipher",
            schema: "identities",
            table: "users",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "proxy_port",
            schema: "identities",
            table: "users",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "proxy_scheme",
            schema: "identities",
            table: "users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "proxy_username",
            schema: "identities",
            table: "users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "verified_via_egress",
            schema: "core",
            table: "exchange_credentials",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "proxy_enabled",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "proxy_host",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "proxy_password_cipher",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "proxy_port",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "proxy_scheme",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "proxy_username",
            schema: "identities",
            table: "users");

        migrationBuilder.DropColumn(
            name: "verified_via_egress",
            schema: "core",
            table: "exchange_credentials");
    }
}
