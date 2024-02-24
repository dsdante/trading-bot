using System.Diagnostics;
using System.Globalization;
using EFCore.NamingConventions.Internal;
using Microsoft.EntityFrameworkCore;

namespace TradingBot.Data;

public class TradingBotDbContext(
    DbContextOptions<TradingBotDbContext> options,
    ILogger<TradingBotDbContext> logger) : DbContext(options)
{
    public required DbSet<Instrument> Instruments { get; init; }
    public required DbSet<Candle> Candles { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssetType>();

        // Singular table names
        var snakeCase = new SnakeCaseNameRewriter(CultureInfo.InvariantCulture);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(snakeCase.RewriteName(entity.DisplayName()));
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
                oldInstrument.First1MinCandleDate = instrument.First1MinCandleDate;
                oldInstrument.First1DayCandleDate = instrument.First1DayCandleDate;
                updated[instrument.Uid] = oldInstrument;
            }
            else
            {
                Debug.Assert(instrument.Id == 0);
                added[instrument.Uid] = instrument;
            }
        }

        await Instruments.AddRangeAsync(added.Values, cancellation);
        Instruments.UpdateRange(updated.Values);

        if (added.Count + updated.Count == 0)
            logger.LogInformation("All instruments up to date.");
        if (added.Count > 0)
            logger.LogInformation("{count} instrument(s) added:\n{list}",
                added.Count, string.Join('\n', added.Values.Select(i => $"{i.AssetType} {i.Name}")));
        if (updated.Count > 0)
            logger.LogInformation("{count} instrument(s) updated:\n{list}",
                updated.Count, string.Join('\n', updated.Values.Select(i => $"{i.AssetType} {i.Name}")));
    }
}
