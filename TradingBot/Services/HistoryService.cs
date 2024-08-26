using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
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
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Started downloading history beginning. Looking for instruments missing history beginning...");

        // Find the earliest candles of the instruments for which we don't have the beginning of history.
        List<(Instrument instrument, DateTime earliest)> earliestCandles = await dbContext.Instruments
            .Where(instrument => !instrument.HasEarliest1MinCandle &&
                                 instrument.Figi != null &&
                                 historyAssetTypes.Contains(instrument.AssetType))
            .SelectMany(instrument => instrument.Candles.Select(candle => candle.Timestamp).DefaultIfEmpty(),
                        (instrument, timestamp) => new { instrument, timestamp })
            .GroupBy(candle => candle.instrument.Id)
            .Select(group => new
            {
                instrumentId = group.Key,
                timestamp = group.Min(candle => candle.timestamp),
            })
            // TODO: Replace Min+Join with MinBy once it's supported in EF. https://github.com/dotnet/efcore/issues/25566
            .Join(dbContext.Instruments,
                  candle => candle.instrumentId,
                  instrument => instrument.Id,
                  (candle, instrument) => new ValueTuple<Instrument, DateTime>(instrument, candle.timestamp))
            .ToListAsync(cancellation);
        if (earliestCandles.Count == 0)
        {
            logger.LogInformation("We already have the beginning of history for all instruments.");
            return;
        }
        logger.LogInformation("{instrumentCount} instruments are missing history beginning.", earliestCandles.Count);

        PriorityQueue<(Instrument instrument, int year), Priority> queue = new(earliestCandles.Count);
        int yearToday = DateTime.UtcNow.Year;
        foreach (var (instrument, timestamp) in earliestCandles)
        {
            int year = timestamp != default ? timestamp.Year - 1 : yearToday - 1;
            queue.Enqueue((instrument, year), Priority.Normal);
        }

        while (queue.TryDequeue(out var instrumentAndYear, out var priority))
        {
            var (instrument, year) = instrumentAndYear;
            var response = await tInvestHistoryData.DownloadCsvAsync(instrument, year, cancellation);

            if (response.IsSuccessStatusCode)
            {
                // Prioritize keeping downloading the same instrument.
                queue.Enqueue((instrument, year - 1), Priority.High);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound ||
                     response.StatusCode == HttpStatusCode.InternalServerError)
            {
                // Reached the beginning of the history.
                logger.LogInformation("{assetType} {instrument} ({year}): reached beginning of history.",
                    instrument.AssetType, instrument.Name, year + 1);
                await dbContext.Instruments
                    .Where(i => i.Id == instrument.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.HasEarliest1MinCandle, true), cancellation);
            }
            else
            {
                if (priority != Priority.Low)
                {
                    // At the end of the queue, give a second chance to the failed instruments.
                    queue.Enqueue((instrument, year), Priority.Low);
                }
                else
                {
                    // No more second chances.
                    logger.LogError("{assetType} {instrument} ({year}): second chance failed with {status}.",
                        instrument.AssetType, instrument.Name, year, response.StatusCode);
                }
            }

            if (queue.Count > 0)
                await response.WaitAsync(LogRateLimit, cancellation);
        }

        logger.LogInformation(@"Finished downloading history beginning in {time:h\:mm\:ss}.", stopwatch.Elapsed);
    }

    /// <summary> Update the recent candle history from T-Invest API </summary>
    public async Task UpdateHistoryAsync(CancellationToken cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Started updating the recent history. Looking for instruments requiring a history update...");

        // Find the latest candles for each instrument.
        DateTime startOfYear = new(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        List<(Instrument instrument, DateTime latest)> latestCandles = await dbContext.Instruments
            .Where(instrument => instrument.Figi != null && historyAssetTypes.Contains(instrument.AssetType))
            .SelectMany(instrument => instrument.Candles.Select(candle => (DateTime?)candle.Timestamp).DefaultIfEmpty(),
                        (instrument, timestamp) => new { instrument, timestamp })
            .GroupBy(candle => candle.instrument.Id)
            .Select(group => new
            {
                instrumentId = group.Key,
                timestamp = group.Max(candle => candle.timestamp) ?? startOfYear,
            })
            .Where(candle => candle.timestamp < DateTime.UtcNow.AddDays(-1))
            .OrderBy(candle => candle.timestamp)
            // TODO: Replace Max+Join with MaxBy once it's supported in EF. https://github.com/dotnet/efcore/issues/25566
            .Join(dbContext.Instruments,
                  candle => candle.instrumentId,
                  instrument => instrument.Id,
                  (candle, instrument) => new ValueTuple<Instrument, DateTime>(instrument, candle.timestamp))
            .ToListAsync(cancellation);
        logger.LogInformation("{instrumentCount} instruments need updating.", latestCandles.Count);
        if (latestCandles.Any(candle => !candle.instrument.HasEarliest1MinCandle))
            logger.LogWarning("We don't have the beginning of history for the instruments:{instrumentList}",
                Environment.NewLine + string.Join(Environment.NewLine, latestCandles
                    .Select(candle => candle.instrument)
                    .Where(instrument => !instrument.HasEarliest1MinCandle)
                    .OrderBy(instrument => instrument.AssetType)
                    .ThenBy(instrument => instrument.Name)
                    .Select(instrument => $"{instrument.AssetType} {instrument.Name}")));
        if (latestCandles.Count == 0)
        {
            logger.LogInformation("All candle history is up to date.");
            return;
        }
        logger.LogInformation("{instrumentCount} instruments are missing history beginning.", latestCandles.Count);

        PriorityQueue<(Instrument instrument, int year), Priority> queue = new(latestCandles.Count);
        var yearToday = DateTime.UtcNow.Year;
        foreach (var (instrument, timestamp) in latestCandles)
        {
            // Download earlier years with a higher priority.
            var year = timestamp.Year;
            var priority = year < yearToday ? Priority.High : Priority.Normal;
            queue.Enqueue((instrument, year), priority);
        }

        while (queue.TryDequeue(out var instrumentAndYear, out var priority))
        {
            var (instrument, year) = instrumentAndYear;
            var response = await tInvestHistoryData.DownloadCsvAsync(instrument, year, cancellation);

            if (response.IsSuccessStatusCode)
            {
                yearToday = DateTime.UtcNow.Year;
                if (year != yearToday)
                {
                    // Download the current year later in the queue, to allow more data to be included.
                    priority = year + 1 < yearToday ? Priority.High : Priority.Normal;
                    queue.Enqueue((instrument, year + 1), Priority.High);
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound ||
                     response.StatusCode == HttpStatusCode.InternalServerError)
            {
                // History ends here.
                logger.LogInformation("{assetType} {instrument} ({year}): no more history.",
                    instrument.AssetType, instrument.Name, year);
            }
            else
            {
                if (priority != Priority.Low)
                {
                    // At the end of the queue, give a second chance to the failed instruments.
                    queue.Enqueue((instrument, year), Priority.Low);
                }
                else
                {
                    // No more second chances.
                    logger.LogError("{assetType} {instrument} ({year}): second chance failed with {status}.",
                        instrument.AssetType, instrument.Name, year, response.StatusCode);
                }
            }

            if (queue.Count > 0)
                await response.WaitAsync(LogRateLimit, cancellation);
        }

        logger.LogInformation(@"Finished updating the recent history in {time:h\:mm\:ss}.", stopwatch.Elapsed);
    }

    private enum Priority
    {
        High,
        Normal,
        Low,
    }

    private static readonly AssetType[] historyAssetTypes =
    [
        AssetType.Bond,
        AssetType.Currency,
        AssetType.Share,
        AssetType.Etf
    ];

    private void LogRateLimit(TimeSpan timeout) =>
        logger.LogDebug("Rate limit reached, waiting for {reset:0.#} seconds.", timeout.TotalSeconds);
}
