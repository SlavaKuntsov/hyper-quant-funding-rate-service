using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FRC_Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchanges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funding_rates_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "text", nullable: false),
                    interval_hours = table.Column<int>(type: "integer", nullable: false),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    open_interest = table.Column<decimal>(type: "numeric", nullable: false),
                    ts_rate = table.Column<long>(type: "bigint", nullable: false),
                    fetched_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funding_rates_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_funding_rates_history_exchanges_exchange_id",
                        column: x => x.exchange_id,
                        principalTable: "exchanges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "funding_rates_online",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "text", nullable: false),
                    exchange_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    open_interest = table.Column<decimal>(type: "numeric", nullable: false),
                    ts_rate = table.Column<long>(type: "bigint", nullable: false),
                    fetched_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funding_rates_online", x => x.id);
                    table.ForeignKey(
                        name: "fk_funding_rates_online_exchanges_exchange_id",
                        column: x => x.exchange_id,
                        principalTable: "exchanges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fr_history",
                table: "funding_rates_history",
                columns: new[] { "symbol", "exchange_id", "ts_rate" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_funding_rates_history_exchange_id",
                table: "funding_rates_history",
                column: "exchange_id");

            migrationBuilder.CreateIndex(
                name: "ix_fr_online",
                table: "funding_rates_online",
                columns: new[] { "symbol", "exchange_id", "ts_rate" },
                unique: true,
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_funding_rates_online_exchange_id",
                table: "funding_rates_online",
                column: "exchange_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funding_rates_history");

            migrationBuilder.DropTable(
                name: "funding_rates_online");

            migrationBuilder.DropTable(
                name: "exchanges");
        }
    }
}
