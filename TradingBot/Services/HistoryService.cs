using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using TradingBot.Data;

namespace TradingBot;

public partial class HistoryService(
    ITInvestService tInvest,
    ITInvestHistoryDataService tInvestHistoryData,
    TradingBotDbContext dbContext,
    ILogger<HistoryService> logger)
{
    /// <summary> Refresh instrument info from T-Invest API </summary>
    public async Task UpdateInstrumentsAsync(CancellationToken cancellation)
    {
        logger.LogInformation("Updating the instruments...");

        List<Instrument> instruments = [];
        await foreach (var instrument in tInvest.GetInstrumentsAsync(cancellation))
            instruments.Add(instrument);

        await dbContext.UpsertRangeAsync(instruments, cancellation);
        await dbContext.SaveChangesAsync(cancellation);
    }

    /// <summary> Download the complete candle history from T-Invest API, excluding the current year </summary>
    public async Task DownloadHistoryBeginningAsync(CancellationToken cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Started downloading history beginning. Looking for instruments missing history beginning...");

        // Find the earliest candles of the instruments for which we don't have the beginning of history.
        List<(Instrument instrument, int earliest)> earliestCandles = await dbContext.Instruments
            .Where(instrument =>
                !instrument.HasEarliest1MinCandle &&
                instrument.Figi != null &&
                historyAssetTypes.Contains(instrument.AssetType))
            .SelectMany(
                instrument => instrument.Candles.Select(candle => candle.TimestampMinutes).DefaultIfEmpty(),
                (instrument, timestamp) => new { instrument, timestamp })
            .GroupBy(candle => candle.instrument.Id)
            .Select(group => new
            {
                instrumentId = group.Key,
                timestamp = group.Min(candle => candle.timestamp),
            })
            // TODO: Replace Min+Join with MinBy once it's supported in EF. https://github.com/dotnet/efcore/issues/25566
            .Join(
                dbContext.Instruments,
                candle => candle.instrumentId,
                instrument => instrument.Id,
                (candle, instrument) => new ValueTuple<Instrument, int>(instrument, candle.timestamp))
            .ToListAsync(cancellation);

        if (earliestCandles.Count == 0)
        {
            logger.LogInformation("No instruments are missing history beginning.");
            return;
        }
        LogBeginningOfHistoryNeeded(earliestCandles.Count);

        PriorityQueue<(Instrument instrument, int year), Priority> queue = new(earliestCandles.Count);
        int yearToday = DateTime.UtcNow.Year;
        foreach (var (instrument, timestamp) in earliestCandles)
        {
            int year = timestamp == 0 ? yearToday - 1 : Candle.ToDateTime(timestamp).Year - 1;
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
            else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.InternalServerError)
            {
                // Reached the beginning of the history.
                LogBeginningOfHistory(instrument.AssetType, instrument.Name, year + 1);
                await dbContext.Instruments
                    .Where(i => i.Id == instrument.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(i => i.HasEarliest1MinCandle, true),
                        cancellation);
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
                    LogSecondChanceFailed(instrument.AssetType, instrument.Name, year, response.StatusCode);
                }
            }

            if (queue.Count > 0)
                await response.WaitAsync(LogRateLimit, cancellation);
        }

        LogFinishedDownloading(stopwatch.Elapsed);
    }

    /// <summary> Update the recent candle history from T-Invest API </summary>
    public async Task UpdateHistoryAsync(CancellationToken cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Started updating the recent history. Looking for the instruments requiring a history update...");

        // Find the latest candles for each instrument.
        var yearToday = DateTime.UtcNow.Year;
        int yesterday = Candle.ToMinutes(DateTime.UtcNow.AddDays(-1), round: true);

        List<(Instrument instrument, int latest)> latestCandles = await dbContext.Instruments
            .Where(instrument =>
                instrument.Figi != null &&
                historyAssetTypes.Contains(instrument.AssetType))
            .SelectMany(
                instrument => instrument.Candles.Select(candle => candle.TimestampMinutes).DefaultIfEmpty(),
                (instrument, timestamp) => new { instrument, timestamp })
            .GroupBy(candle => candle.instrument.Id)
            .Select(group => new
            {
                instrumentId = group.Key,
                timestamp = group.Max(candle => candle.timestamp),
            })
            .Where(candle => candle.timestamp < yesterday)
            .OrderBy(candle => candle.timestamp)
            // TODO: Replace Max+Join with MaxBy when it's supported in EF. https://github.com/dotnet/efcore/issues/25566
            .Join(
                dbContext.Instruments,
                candle => candle.instrumentId,
                instrument => instrument.Id,
                (candle, instrument) => new ValueTuple<Instrument, int>(instrument, candle.timestamp))
            .ToListAsync(cancellation);

        if (latestCandles.Count == 0)
        {
            logger.LogInformation("No instruments need updating.");
            return;
        }

#pragma warning disable CA1873  // TODO: Remove when fixed https://github.com/dotnet/roslyn-analyzers/issues/7690
        if (latestCandles.Any(candle => !candle.instrument.HasEarliest1MinCandle))
        {
            LogBeginningOfHistoryNotFound(latestCandles
                .Select(candle => candle.instrument)
                .Where(instrument => !instrument.HasEarliest1MinCandle)
                .OrderBy(instrument => instrument.AssetType)
                .ThenBy(instrument => instrument.Name)
                .Select(instrument => $"{instrument.AssetType} {instrument.Name}"));
        }
#pragma warning restore CA1873

        LogInstrumentsNeedUpdating(latestCandles.Count);

        PriorityQueue<(Instrument instrument, int year), Priority> queue = new(latestCandles.Count);
        foreach (var (instrument, timestamp) in latestCandles)
        {
            // Download earlier years with a higher priority.
            var year = timestamp == 0 ? yearToday : Candle.ToDateTime(timestamp).Year;
            var priority = year < yearToday ? Priority.High : Priority.Normal;
            queue.Enqueue((instrument, year), priority);
        }

        while (queue.TryDequeue(out var instrumentAndYear, out var priority))
        {
            var (instrument, year) = instrumentAndYear;
            var response = await tInvestHistoryData.DownloadCsvAsync(instrument, year, cancellation);

            if (response.IsSuccessStatusCode)
            {
                if (year != DateTime.UtcNow.Year)
                {
                    // Download the current year later in the queue, to allow more data to be included.
                    var nextPriority = year + 1 < DateTime.UtcNow.Year ? Priority.High : Priority.Normal;
                    queue.Enqueue((instrument, year + 1), nextPriority);
                }
            }
            else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.InternalServerError)
            {
                // History ends here.
                LogEndOfHistory(instrument.AssetType, instrument.Name, year);
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
                    LogSecondChanceFailed(instrument.AssetType, instrument.Name, year, response.StatusCode);
                }
            }

            if (queue.Count > 0)
                await response.WaitAsync(LogRateLimit, cancellation);
        }

        LogFinishedHistoryUpdate(stopwatch.Elapsed);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "{instrumentCount} instrument(s) need updating.")]
    private partial void LogInstrumentsNeedUpdating(int instrumentCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "{instrumentCount} instrument(s) are missing history beginning yet.")]
    private partial void LogBeginningOfHistoryNeeded(int instrumentCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "History beginning is not found for the instruments: {instruments}")]
    private partial void LogBeginningOfHistoryNotFound(IEnumerable<string> instruments);

    [LoggerMessage(Level = LogLevel.Information, Message = "{assetType} {instrument} ({year}): reached history beginning.")]
    private partial void LogBeginningOfHistory(AssetType assetType, string instrument, int year);

    [LoggerMessage(Level = LogLevel.Information, Message = "{assetType} {instrument} ({year}): no more history.")]
    private partial void LogEndOfHistory(AssetType assetType, string instrument, int year);

    [LoggerMessage(Level = LogLevel.Debug, Message = @"Rate limit reached, waiting for {time:s\\.f} seconds.")]
    private partial void LogRateLimit(TimeSpan time);

    [LoggerMessage(Level = LogLevel.Error, Message = "{assetType} {instrument} ({year}): second chance failed with {status}.")]
    private partial void LogSecondChanceFailed(AssetType assetType, string instrument, int year, HttpStatusCode status);

    [LoggerMessage(Level = LogLevel.Information, Message = @"Finished downloading history beginning in {time:h\\:mm\\:ss}.")]
    private partial void LogFinishedDownloading(TimeSpan time);

    [LoggerMessage(Level = LogLevel.Information, Message = @"Finished updating the recent history in {time:h\\:mm\\:ss}.")]
    private partial void LogFinishedHistoryUpdate(TimeSpan time);
}
