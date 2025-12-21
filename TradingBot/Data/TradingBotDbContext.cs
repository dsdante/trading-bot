using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

namespace TradingBot.Data;

public partial class TradingBotDbContext(
        DbContextOptions<TradingBotDbContext> options,
        ILogger<TradingBotDbContext> logger)
    : DbContext(options)
{
    public DbSet<Instrument> Instruments { get; init; }
    public DbSet<Candle> Candles { get; init; }
    public DbSet<Feature> Feature { get; init; }
    public DbSet<Split> Splits { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssetType>();

        // Singular table names
        SnakeCaseNameRewriter snakeCase = new(CultureInfo.InvariantCulture);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(snakeCase.RewriteName(entity.DisplayName()));

        modelBuilder.Entity<Candle>().ToTable(table =>
        {
            table.HasCheckConstraint("candle_open_check", "open BETWEEN low AND high");
            table.HasCheckConstraint("candle_close_check", "close BETWEEN low AND high");
            table.HasCheckConstraint("candle_volume_check", "volume >= 0");
        });
    }

    /// <summary> Add new instruments and update those with existing UIDs </summary>
    public async Task UpsertRangeAsync(IEnumerable<Instrument> instruments, CancellationToken cancellation)
    {
        var oldInstruments = await Instruments.ToDictionaryAsync(i => i.Uid, i => i, cancellation);
        Dictionary<Guid, Instrument> added = [];
        Dictionary<Guid, Instrument> updated = [];

        foreach (var instrument in instruments)
        {
            if (oldInstruments.TryGetValue(instrument.Uid, out var oldInstrument))
            {
                if (oldInstrument == instrument)
                    continue;
                oldInstrument.AssetType = instrument.AssetType;
                oldInstrument.Name = instrument.Name;
                oldInstrument.Ticker = instrument.Ticker;
                oldInstrument.Figi = instrument.Figi;
                oldInstrument.Lot = instrument.Lot;
                oldInstrument.Country = instrument.Country;
                oldInstrument.Otc = instrument.Otc;
                oldInstrument.Qual = instrument.Qual;
                oldInstrument.ApiTradeAvailable = instrument.ApiTradeAvailable;
                oldInstrument.HasEarliest1MinCandle |= instrument.HasEarliest1MinCandle;
                updated.Add(instrument.Uid, oldInstrument);
            }
            else
            {
                Debug.Assert(instrument.Id == 0, "Cannot upsert instruments with a specified (non-zero) ID.");
                added.Add(instrument.Uid, instrument);
            }
        }

        // TODO: Race condition. Use raw SQL?
        if (added.Count > 0)
            await Instruments.AddRangeAsync(added.Values, cancellation);
        if (updated.Count > 0)
            Instruments.UpdateRange(updated.Values);

#pragma warning disable CA1873  // TODO: Remove when fixed https://github.com/dotnet/roslyn-analyzers/issues/7690
        if (logger.IsEnabled(LogLevel.Information))
        {
            if (added.Count + updated.Count == 0)
                logger.LogInformation("All instruments are up to date.");
            if (added.Count > 0)
                LogAddedInstruments(added.Count, added.Values.Select(i => $"{i.AssetType} {i.Name}"));
            if (updated.Count > 0)
                LogUpdatedInstruments(updated.Count, updated.Values.Select(i => $"{i.AssetType} {i.Name}"));
        }
#pragma warning restore CA1873
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{count} instrument(s) added: {instruments}")]
    private partial void LogAddedInstruments(int count, IEnumerable<string> instruments);

    [LoggerMessage(Level = LogLevel.Information, Message = "{count} instrument(s) updated: {instruments}")]
    private partial void LogUpdatedInstruments(int count, IEnumerable<string> instruments);
}
