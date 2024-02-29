using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;

namespace TradingBot;

public class HistoryService(
    TinkoffService tinkoff,
    TinkoffHistoryDataService tinkoffHistoryData,
    TradingBotDbContext dbContext,
    ILogger<HistoryService> logger)
{
    /// <summary> Refresh instrument info from Tinkoff API </summary>
    public async Task UpdateInstruments(CancellationToken cancellation)
    {
        logger.LogInformation("Updating the instruments...");

        var instruments = new List<Instrument>();
        await foreach (var instrument in tinkoff.GetInstruments(cancellation))
            instruments.Add(instrument);

        await dbContext.UpsertRangeAsync(instruments, cancellation);
        await dbContext.SaveChangesAsync(cancellation);
    }

    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public async Task DownloadHistory(CancellationToken cancellation)
    {
        var assetTypes = new[] { AssetType.Bond, AssetType.Currency, AssetType.Share, AssetType.Etf };

        // <instrument, earliest candle timestamp>
        var earliestCandles = await dbContext.Candles
            .Where(candle => dbContext.Instruments
                .Where(instrument => assetTypes.Contains(instrument.AssetType))
                .Select(instrument => instrument.Id)
                .Contains(candle.InstrumentId))
            .GroupBy(candle => candle.InstrumentId)
            .Select(group => new
            {
                instrumentId = group.Key,
                firstTimestamp = group.Min(candle => candle.Timestamp),
            })
            .Join(dbContext.Instruments,
                candle => candle.instrumentId,
                instrument => instrument.Id,
                (candle, instrument) => new { instrument, candle.firstTimestamp })
            .AsNoTracking()
            .ToDictionaryAsync(
                pair => pair.instrument,
                pair => pair.firstTimestamp,
                cancellation);

        var (instrument, earliest) = earliestCandles.First();
        logger.LogInformation("{instrument} {earliest:yyyy-MM-dd HH:mm:ss K}", instrument, earliest);

        for (int year = DateTime.UtcNow.Year; ; year--)
        {
            var (status, limit, limitTimeout) = await tinkoffHistoryData.DownloadCsvAsync(instrument, year, cancellation);
            if (status != HttpStatusCode.OK)
                break;
            logger.LogInformation("Limit: {limit}; timeout: {timeout:HH:mm:ss.fff K}.", limit, limitTimeout);
        }
    }
}
