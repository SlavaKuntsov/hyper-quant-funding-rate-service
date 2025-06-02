using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FRC_Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fr_online",
                table: "funding_rates_online");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "funding_rates_online",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "funding_rates_history",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_fr_online_name_exchange_unique",
                table: "funding_rates_online",
                columns: new[] { "name", "exchange_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fr_online_symbol",
                table: "funding_rates_online",
                column: "symbol");

            migrationBuilder.CreateIndex(
                name: "ix_fr_online_symbol_exchange_unique",
                table: "funding_rates_online",
                columns: new[] { "symbol", "exchange_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fr_online_tsrate",
                table: "funding_rates_online",
                column: "ts_rate",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fr_online_name_exchange_unique",
                table: "funding_rates_online");

            migrationBuilder.DropIndex(
                name: "ix_fr_online_symbol",
                table: "funding_rates_online");

            migrationBuilder.DropIndex(
                name: "ix_fr_online_symbol_exchange_unique",
                table: "funding_rates_online");

            migrationBuilder.DropIndex(
                name: "ix_fr_online_tsrate",
                table: "funding_rates_online");

            migrationBuilder.DropColumn(
                name: "name",
                table: "funding_rates_online");

            migrationBuilder.DropColumn(
                name: "name",
                table: "funding_rates_history");

            migrationBuilder.CreateIndex(
                name: "ix_fr_online",
                table: "funding_rates_online",
                columns: new[] { "symbol", "exchange_id", "ts_rate" },
                unique: true,
                descending: new[] { false, false, true });
        }
    }
}
