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

    [SuppressMessage("ReSharper", "EntityFramework.UnsupportedServerSideFunctionCall")]
    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public async Task DownloadHistory(CancellationToken cancellation)
    {
        var assetTypes = new[] { AssetType.Bond, AssetType.Currency, AssetType.Share, AssetType.Etf };

        // <instrument, earliest candle timestamp (null if no candles)>
        var earliestCandles = await dbContext.Instruments
            .Where(instrument => assetTypes.Contains(instrument.AssetType))
            .SelectMany(instrument => instrument.Candles
                                                .Select(candle => (DateTime?)candle.Timestamp)
                                                .DefaultIfEmpty(),
                        (instrument, timestamp) => new { instrumentId = instrument.Id, timestamp })
            .GroupBy(candle => candle.instrumentId)
            .Select(group => new
            {
                instrumentId = group.Key,
                earliestTimestamp = group.Min(candle => candle.timestamp),
            })
            .Join(dbContext.Instruments,
                  pair => pair.instrumentId,
                  instrument => instrument.Id,
                  (pair, instrument) => new { instrument, pair.earliestTimestamp })
            .ToDictionaryAsync(pair => pair.instrument,
                               pair => pair.earliestTimestamp,
                               cancellation);


        var (instrument, earliest) = earliestCandles.First(pair => pair.Key.Name == "Роснефть");
        if (earliest != null)
            logger.LogInformation("{instrument} {earliest:yyyy-MM-dd HH:mm:ss K}", instrument, earliest);
        else
            logger.LogInformation("{instrument}, no candles yet.", instrument);

        for (int year = DateTime.UtcNow.Year; ; year--)
        {
            var (status, limit, limitTimeout) = await tinkoffHistoryData.DownloadCsvAsync(instrument, year, cancellation);
            if (status == HttpStatusCode.NotFound)
            {
                var updatedInstrument = await dbContext.Instruments
                    .FirstAsync(i => i.Id == instrument.Id, CancellationToken.None);
                updatedInstrument.HasEarliest1MinCandle = true;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            if (status != HttpStatusCode.OK)
                break;
            logger.LogInformation("Limit: {limit}; timeout: {timeout:HH:mm:ss.fff K}.", limit, limitTimeout);
        }
    }
}
