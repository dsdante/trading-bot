using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TradingBot.Data;

#nullable disable

namespace TradingBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:asset_type", "bond,currency,etf,future,option,share");

            migrationBuilder.CreateTable(
                name: "instrument",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uid = table.Column<Guid>(type: "uuid", nullable: false),
                    figi = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    asset_type = table.Column<AssetType>(type: "asset_type", nullable: false),
                    lot = table.Column<int>(type: "integer", nullable: false),
                    otc_flag = table.Column<bool>(type: "boolean", nullable: false),
                    for_qual_investor_flag = table.Column<bool>(type: "boolean", nullable: false),
                    api_trade_available_flag = table.Column<bool>(type: "boolean", nullable: false),
                    first1min_candle_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    first1day_candle_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candle",
                columns: table => new
                {
                    instrument = table.Column<short>(type: "smallint", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    open = table.Column<float>(type: "real", nullable: false),
                    high = table.Column<float>(type: "real", nullable: false),
                    low = table.Column<float>(type: "real", nullable: false),
                    close = table.Column<float>(type: "real", nullable: false),
                    volume = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candle", x => new { x.instrument, x.timestamp });
                    table.ForeignKey(
                        name: "fk_candle_instruments_instrument",
                        column: x => x.instrument,
                        principalTable: "instrument",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_uid",
                table: "instrument",
                column: "uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candle");

            migrationBuilder.DropTable(
                name: "instrument");
        }
    }
}
