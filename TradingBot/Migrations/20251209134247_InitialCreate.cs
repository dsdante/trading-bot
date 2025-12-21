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
                    asset_type = table.Column<AssetType>(type: "asset_type", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    ticker = table.Column<string>(type: "text", nullable: true),
                    figi = table.Column<string>(type: "text", nullable: true),
                    uid = table.Column<Guid>(type: "uuid", nullable: false),
                    lot = table.Column<int>(type: "integer", nullable: false),
                    country = table.Column<string>(type: "text", nullable: true),
                    otc = table.Column<bool>(type: "boolean", nullable: false),
                    qual = table.Column<bool>(type: "boolean", nullable: false),
                    api_trade_available = table.Column<bool>(type: "boolean", nullable: false),
                    has_earliest_1min_candle = table.Column<bool>(type: "boolean", nullable: false)
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
                    timestamp = table.Column<int>(type: "integer", nullable: false),
                    open = table.Column<float>(type: "real", nullable: false),
                    high = table.Column<float>(type: "real", nullable: false),
                    low = table.Column<float>(type: "real", nullable: false),
                    close = table.Column<float>(type: "real", nullable: false),
                    volume = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candle", x => new { x.instrument, x.timestamp });
                    table.CheckConstraint("candle_close_check", "close BETWEEN low AND high");
                    table.CheckConstraint("candle_open_check", "open BETWEEN low AND high");
                    table.CheckConstraint("candle_volume_check", "volume >= 0");
                    table.ForeignKey(
                        name: "fk_candle_instruments_instrument",
                        column: x => x.instrument,
                        principalTable: "instrument",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature",
                columns: table => new
                {
                    instrument = table.Column<short>(type: "smallint", nullable: false),
                    timestamp = table.Column<int>(type: "integer", nullable: false),
                    lag = table.Column<float>(type: "real", nullable: false),
                    gap = table.Column<float>(type: "real", nullable: false),
                    volume = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature", x => new { x.instrument, x.timestamp });
                    table.ForeignKey(
                        name: "fk_feature_instruments_instrument",
                        column: x => x.instrument,
                        principalTable: "instrument",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "split",
                columns: table => new
                {
                    instrument = table.Column<short>(type: "smallint", nullable: false),
                    timestamp = table.Column<int>(type: "integer", nullable: false),
                    split = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_split", x => new { x.instrument, x.timestamp });
                    table.ForeignKey(
                        name: "fk_split_instrument_instrument",
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
                name: "feature");

            migrationBuilder.DropTable(
                name: "split");

            migrationBuilder.DropTable(
                name: "instrument");
        }
    }
}
