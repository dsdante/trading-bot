using Microsoft.EntityFrameworkCore;
using System.Net;
using TradingBot.Data;

namespace TradingBot;

public class HistoryService(
    TInvestService tInvest,
    TInvestHistoryDataService tInvestHistoryData,
    TradingBotDbContext dbContext,
    ILogger<HistoryService> logger)
{
    /// <summary> Refresh instrument info from T-Invest API </summary>
    public async Task UpdateInstrumentsAsync(CancellationToken cancellation)
    {
        logger.LogInformation("Updating the instruments...");

        var instruments = new List<Instrument>();
        await foreach (var instrument in tInvest.GetInstrumentsAsync(cancellation))
            instruments.Add(instrument);

        await dbContext.UpsertRangeAsync(instruments, cancellation);
        await dbContext.SaveChangesAsync(cancellation);
    }

    /// <summary> Download the complete candle history from T-Invest API, excluding the current year </summary>
    public async Task DownloadHistoryBeginningAsync(CancellationToken cancellation)
    {
        logger.LogInformation("Started downloading history beginning.");
        var assetTypes = new[] { AssetType.Bond, AssetType.Currency, AssetType.Share, AssetType.Etf };

        // Find the earliest candles of the instruments for which we don't have the beginning of history.
        List<(Instrument instrument, DateTime earliest)> earliestCandles = await dbContext.Instruments
            .Where(instrument => !instrument.HasEarliest1MinCandle &&
                                 instrument.Figi != null &&
                                 assetTypes.Contains(instrument.AssetType))
            .SelectMany(instrument => instrument.Candles.Select(candle => candle.Timestamp).DefaultIfEmpty(),
                        (instrument, timestamp) => new { instrument, timestamp })
            .GroupBy(candle => candle.instrument.Id)
            .Select(group => new
            {
                instrumentId = group.Key,
                timestamp = group.Min(candle => candle.timestamp)
            })
            // TODO: Replace Min+Join with MinBy once it's supported in EF. https://github.com/dotnet/efcore/issues/25566
            .Join(dbContext.Instruments,
                  candle => candle.instrumentId,
                  instrument => instrument.Id,
                  (candle, instrument) => new ValueTuple<Instrument, DateTime>(instrument, candle.timestamp))
            .ToListAsync(cancellation);
        logger.LogInformation("{instrumentCount} instruments are missing history beginning.", earliestCandles.Count);

        PriorityQueue<(Instrument instrument, int year), Priority> queue = new(earliestCandles.Count);
        int yearToday = DateTime.UtcNow.Year;
        foreach (var (instrument, timestamp) in earliestCandles)
        {
            // Download the current year at the very end to allow more data to be included.
            int year = timestamp != default ? timestamp.Year - 1 : yearToday - 1;
            queue.Enqueue((instrument, year), Priority.InitiallyEnqueued);
        }

        while (queue.TryDequeue(out var instrumentAndYear, out var priority))
        {
            var (instrument, year) = instrumentAndYear;
            var response = await tInvestHistoryData.DownloadCsvAsync(instrument, year, cancellation);

            if (response.IsSuccessStatusCode)
            {
                queue.Enqueue((instrument, year - 1), Priority.CurrentInstrument);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("{assetType} {instrument} ({year}): end of history.",
                    instrument.AssetType, instrument.Name, year + 1);
                await dbContext.Instruments
                    .Where(i => i.Id == instrument.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.HasEarliest1MinCandle, true), cancellation);
            }
            else
            {
                if (priority == Priority.SecondChance)
                {
                    logger.LogError("{assetType} {instrument} ({year}): second chance failed with {status}.",
                        instrument.AssetType, instrument.Name, year, response.StatusCode);
                }
                else
                {
                    queue.Enqueue((instrument, year), Priority.SecondChance);
                }
            }

            if (response.Remaining == 0 && queue.Count > 0)
            {
                var rateLimitTimeout = response.Reset - DateTime.UtcNow;
                if (rateLimitTimeout > TimeSpan.Zero)
                {
                    logger.LogDebug("Rate limit reached, waiting for {reset:0.#} seconds.", rateLimitTimeout.TotalSeconds);
                    await Task.Delay(rateLimitTimeout, cancellation);
                }
            }
        }

        logger.LogInformation("Finished downloading history beginning.");
    }

    private enum Priority
    {
        CurrentInstrument,  // prioritize keeping downloading the same instrument
        InitiallyEnqueued,  // initially enqueued instruments
        SecondChance,  // at the end of the queue, give a second chance to the failed instruments
    }
}
