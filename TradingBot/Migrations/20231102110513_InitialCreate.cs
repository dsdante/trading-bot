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
                    id = table.Column<int>(type: "integer", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instrument");
        }
    }
}
