﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TradingBot.Data;

#nullable disable

namespace TradingBot.Migrations
{
    [DbContext(typeof(TradingBotDbContext))]
    partial class TradingBotDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresEnum(modelBuilder, "asset_type", new[] { "bond", "currency", "etf", "future", "option", "share" });
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TradingBot.Data.Candle", b =>
                {
                    b.Property<short>("InstrumentId")
                        .HasColumnType("smallint")
                        .HasColumnName("instrument");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("timestamp");

                    b.Property<float>("Close")
                        .HasColumnType("real")
                        .HasColumnName("close");

                    b.Property<float>("High")
                        .HasColumnType("real")
                        .HasColumnName("high");

                    b.Property<float>("Low")
                        .HasColumnType("real")
                        .HasColumnName("low");

                    b.Property<float>("Open")
                        .HasColumnType("real")
                        .HasColumnName("open");

                    b.Property<int>("Volume")
                        .HasColumnType("integer")
                        .HasColumnName("volume");

                    b.HasKey("InstrumentId", "Timestamp")
                        .HasName("pk_candle");

                    b.ToTable("candle", (string)null);
                });

            modelBuilder.Entity("TradingBot.Data.Instrument", b =>
                {
                    b.Property<short>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("smallint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<short>("Id"));

                    b.Property<bool>("ApiTradeAvailable")
                        .HasColumnType("boolean")
                        .HasColumnName("api_trade_available_flag");

                    b.Property<AssetType>("AssetType")
                        .HasColumnType("asset_type")
                        .HasColumnName("asset_type");

                    b.Property<string>("Figi")
                        .HasColumnType("text")
                        .HasColumnName("figi");

                    b.Property<bool>("ForQualInvestor")
                        .HasColumnType("boolean")
                        .HasColumnName("for_qual_investor_flag");

                    b.Property<bool>("HasEarliest1MinCandle")
                        .HasColumnType("boolean")
                        .HasColumnName("has_earliest_1min_candle");

                    b.Property<int>("Lot")
                        .HasColumnType("integer")
                        .HasColumnName("lot");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<bool>("Otc")
                        .HasColumnType("boolean")
                        .HasColumnName("otc_flag");

                    b.Property<Guid>("Uid")
                        .HasColumnType("uuid")
                        .HasColumnName("uid");

                    b.HasKey("Id")
                        .HasName("pk_instrument");

                    b.HasIndex("Uid")
                        .IsUnique()
                        .HasDatabaseName("ix_instrument_uid");

                    b.ToTable("instrument", (string)null);
                });

            modelBuilder.Entity("TradingBot.Data.Candle", b =>
                {
                    b.HasOne("TradingBot.Data.Instrument", "Instrument")
                        .WithMany("Candles")
                        .HasForeignKey("InstrumentId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired()
                        .HasConstraintName("fk_candle_instruments_instrument");

                    b.Navigation("Instrument");
                });

            modelBuilder.Entity("TradingBot.Data.Instrument", b =>
                {
                    b.Navigation("Candles");
                });
#pragma warning restore 612, 618
        }
    }
}
