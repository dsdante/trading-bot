using CommunityToolkit.HighPerformance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using TradingBot.Data;

namespace TradingBot;

/// <summary> Training and inference </summary>
public partial class MachineLearningService(
    TradingBotDbContext dbContext,
    IOptions<TradingBotOptions> options,
    ILogger<HistoryService> logger)
{
    /// <summary> Save the dataset as a raw Brotli-compressed file. </summary>
    public async Task DumpFeatures(CancellationToken cancellation)
    {
        logger.LogInformation("Dumping features to a file...");
        var stopwatch = Stopwatch.StartNew();

        var instruments = await dbContext.Instruments
            .Where(i =>
                i.ApiTradeAvailable &&
                options.Value.AssetTypes.Contains(i.AssetType) &&
                options.Value.Countries.Contains(i.Country))
            .Select(i => i.Id)
            .Order()
            .ToListAsync(cancellation);

        var instrumentIds = new int[instruments[^1] + 1];
        for (int i = 0; i < instruments.Count; i++)
            instrumentIds[instruments[i]] = i;

        var features = dbContext.Feature
            .Include(f => f.Instrument)
            .Where(f =>
                f.Instrument.ApiTradeAvailable &&
                options.Value.AssetTypes.Contains(f.Instrument.AssetType) &&
                options.Value.Countries.Contains(f.Instrument.Country))
            .OrderBy(f => f.TimestampMinutes)
            .Select(f => new ValueTuple<int, short, float, float, float>(
                f.TimestampMinutes,
                f.InstrumentId,
                f.Lag,
                f.Gap,
                f.Volume))
            .AsAsyncEnumerable()
            .WithCancellation(cancellation);

        var instrumentMd5Hash = MD5.HashData(instrumentIds.AsBytes());
        var instrumentHash = Convert.ToBase64String(instrumentMd5Hash)[..6].Replace('/', '_');
        var tempPath = Path.Combine(
            options.Value.CacheDirectory,
            $"_x{instruments.Count}_{instrumentHash}.br.tmp");

        LogSavingFeatures(tempPath);

        var buffer = new float[instruments.Count * 3];
        var bufferAsBytes = buffer.AsMemory().AsBytes();

        int lastTime = int.MinValue;
        int timeIndex = -1;

        await using (var file = File.OpenWrite(tempPath))
        {
            await using BrotliStream encoder = new(file, CompressionLevel.Fastest);
            // Brotli on CompressionLevel.Fastest is 30 times slower if not buffered.
            await using BufferedStream buffered = new(encoder, (1 << 16) - 16);  // 65520, same as in BrotliStream

            await foreach (var (time, instrument, lag, gap, volume) in features)
            {
                if (time != lastTime)
                {
                    if (timeIndex != -1)
                    {
                        await buffered.WriteAsync(bufferAsBytes, cancellation);
                        Array.Clear(buffer);
                    }

                    lastTime = time;
                    timeIndex++;
                }

                int instrumentIndex = instrumentIds[instrument];
                buffer[instrumentIndex] = lag;
                buffer[instrumentIndex + 1] = gap;
                buffer[instrumentIndex + 2] = volume;
            }

            await buffered.WriteAsync(bufferAsBytes, cancellation);
            timeIndex++;
        }

        var path = Path.Combine(
            options.Value.CacheDirectory,
            $"{timeIndex}x{instruments.Count}_{instrumentHash}.br");
        File.Copy(tempPath, path, overwrite: true);
        File.Delete(tempPath);
        LogFeaturesSaved(timeIndex, instruments.Count, path, stopwatch.Elapsed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = @"Saving feature data to '{path}'...")]
    private partial void LogSavingFeatures(string path);

    [LoggerMessage(Level = LogLevel.Information, Message =
        @"The features of {candles} candles for {instruments} instruments saved to '{path}' in {time:h\\:mm\\:ss}.")]
    private partial void LogFeaturesSaved(int candles, int instruments, string path, TimeSpan time);
}
