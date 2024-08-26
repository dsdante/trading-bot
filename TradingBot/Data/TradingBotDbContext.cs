using System.Diagnostics;
using System.Globalization;
using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

public class TradingBotDbContext(
    DbContextOptions<TradingBotDbContext> options,
    ILogger<TradingBotDbContext> logger) : DbContext(options)
{
    public DbSet<Instrument> Instruments { get; init; } = null!;
    public DbSet<Candle> Candles { get; init; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssetType>();

        // Singular table names
        var snakeCase = new SnakeCaseNameRewriter(CultureInfo.InvariantCulture);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(snakeCase.RewriteName(entity.DisplayName()));

        modelBuilder.Entity<Candle>().ToTable(table =>
            table.HasCheckConstraint("candle_volume_nonnegative_check", "volume >= 0"));
    }

    /// <summary> Add new instruments and update those with existing UIDs </summary>
    public async Task UpsertRangeAsync(IEnumerable<Instrument> instruments, CancellationToken cancellation)
    {
        var oldInstruments = await Instruments.ToDictionaryAsync(i => i.Uid, i => i, cancellation);
        var added = new Dictionary<Guid, Instrument>();
        var updated = new Dictionary<Guid, Instrument>();

        foreach (var instrument in instruments)
        {
            if (oldInstruments.TryGetValue(instrument.Uid, out var oldInstrument))
            {
                if (oldInstrument == instrument)
                    continue;
                oldInstrument.Figi = instrument.Figi;
                oldInstrument.Name = instrument.Name;
                oldInstrument.AssetType = instrument.AssetType;
                oldInstrument.Lot = instrument.Lot;
                oldInstrument.Otc = instrument.Otc;
                oldInstrument.ForQualInvestor = instrument.ForQualInvestor;
                oldInstrument.ApiTradeAvailable = instrument.ApiTradeAvailable;
                oldInstrument.HasEarliest1MinCandle |= instrument.HasEarliest1MinCandle;
                updated[instrument.Uid] = oldInstrument;
            }
            else
            {
                Debug.Assert(instrument.Id == 0, "Cannot upsert instruments with a specified (non-zero) ID.");
                added[instrument.Uid] = instrument;
            }
        }

        // TODO: Race condition

        await Instruments.AddRangeAsync(added.Values, cancellation);
        Instruments.UpdateRange(updated.Values);

        if (added.Count + updated.Count == 0)
            logger.LogInformation("All instruments are up to date.");
        if (added.Count > 0)
            logger.LogInformation("{count} instrument(s) added:\n{list}",  // TODO: replace with Environment.NewLine?
                added.Count, string.Join('\n', added.Values.Select(i => $"{i.AssetType} {i.Name}")));
        if (updated.Count > 0)
            logger.LogInformation("{count} instrument(s) updated:\n{list}",
                updated.Count, string.Join('\n', updated.Values.Select(i => $"{i.AssetType} {i.Name}")));
    }
}
