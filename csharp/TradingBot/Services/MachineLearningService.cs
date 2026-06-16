/*
using CommunityToolkit.HighPerformance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TorchSharp;
using TradingBot.Data;

namespace TradingBot;

/// <summary> Training and inference </summary>
public partial class MachineLearningService(
    TradingBotDbContext dbContext,
    IOptions<TradingBotOptions> options,
    ILogger<HistoryService> logger)
{
    [SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.")]
    public async Task<(torch.Tensor features, torch.Tensor timeEncoding)> LoadFeaturesAsync(
        CancellationToken cancellation)
    {
        logger.LogInformation("Loading features...");
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

        int sizeOfInstrumentFeature = sizeof(float) * 3;  // lag, gap, volume
        int sizeOfTimeRow = 2 + sizeOfInstrumentFeature * instruments.Count;  // time of day encoding + instruments

        var instrumentMd5Hash = MD5.HashData(instrumentIds.AsBytes());
        var instrumentHash = Convert.ToBase64String(instrumentMd5Hash)[..6].Replace('/', '_');
        var cachePath = Directory.EnumerateFiles(
                options.Value.CacheDirectory,
                $"*x{instruments.Count}_{instrumentHash}.br")
            .Max(StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering));

        long timeSeriesLength = 0;
        if (cachePath != null)
        {
            _ = long.TryParse(Regex.Match(Path.GetFileName(cachePath), @"^[0-9]+").Value, out timeSeriesLength);
            long cacheLength = new FileInfo(cachePath).Length;
            if (timeSeriesLength == 0 || cacheLength != timeSeriesLength * sizeOfTimeRow)
            {
                LogCorruptedCacheFile(cachePath);
                timeSeriesLength = 0;
            }
        }

        var featureQuery = dbContext.Feature
            .Include(f => f.Instrument)
            .Where(f =>
                f.Instrument.ApiTradeAvailable &&
                options.Value.AssetTypes.Contains(f.Instrument.AssetType) &&
                options.Value.Countries.Contains(f.Instrument.Country));


        if (timeSeriesLength == 0)
        {
            logger.LogInformation("Loading features from the database...");

            timeSeriesLength = await featureQuery
                .Select(f => f.Timestamp)
                .Distinct()
                .CountAsync(cancellation);

            var featureTensor = torch.zeros(timeSeriesLength, instruments.Count, 3, requires_grad: true);
            var timeTensor = torch.empty(timeSeriesLength, 2);  // cyclic time of day encoding

            var features = featureQuery
                .OrderBy(f => f.TimestampMinutes)
                .Select(f => new ValueTuple<int, short, float, float, float>(
                    f.TimestampMinutes,
                    f.InstrumentId,
                    f.Lag,
                    f.Gap,
                    f.Volume))
                .AsAsyncEnumerable()
                .WithCancellation(cancellation);

            int lastTime = int.MinValue;
            int timeIndex = -1;
            torch.Tensor featureSlice = featureTensor[0];
            var buffer = new float[2 + instruments.Count * 3];
            var bufferAsBytes = buffer.AsMemory().AsBytes();

            await foreach (var (time, instrument, lag, gap, volume) in features)
            {
                if (time != lastTime)
                {
                    lastTime = time;
                    timeIndex++;
                    (timeTensor[timeIndex, 0], timeTensor[timeIndex, 1]) =
                        MathF.SinCos(time % (60 * 24) * (2 * MathF.PI / (60 * 24)));
                    featureSlice = featureTensor[timeIndex];
                }

                int instrumentIndex = instrumentIds[instrument];
                featureSlice[instrumentIndex, 0] = lag;
                featureSlice[instrumentIndex, 1] = gap;
                featureSlice[instrumentIndex, 2] = volume;
            }


            //await buffered.WriteAsync(bufferAsBytes, cancellation);
            timeIndex++;



            var tempCachePath = Path.Combine(options.Value.CacheDirectory, $"_x{instruments.Count}_{instrumentHash}.br.tmp");
            await using var file = File.OpenWrite(tempCachePath);
            await using BrotliStream encoder = new(file, CompressionLevel.Fastest);
            // Brotli on CompressionLevel.Fastest is 30 times slower if not buffered.
            await using BufferedStream buffered = new(encoder, (1 << 16) - 16);  // 65520, same as in BrotliStream
            LogSavingFeatures(tempCachePath);

        }
        else
        {
            LogLoadingFromCache(cachePath!);

            var features = torch.empty(
                timeSeriesLength, instruments.Count, 3,
                names: ["time", "instrument", "feature"],
                requires_grad: true);
        }
    }

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

    [LoggerMessage(Level = LogLevel.Information, Message = @"Loading feature data from cache '{path}'...")]
    private partial void LogLoadingFromCache(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = @"Saving feature data to '{path}'...")]
    private partial void LogSavingFeatures(string path);

    [LoggerMessage(Level = LogLevel.Information, Message =
        @"The features of {candles} candles for {instruments} instruments saved to '{path}' in {time:h\\:mm\\:ss}.")]
    private partial void LogFeaturesSaved(int candles, int instruments, string path, TimeSpan time);

    [LoggerMessage(Level = LogLevel.Warning, Message = @"Cache file '{path}' is corrupted. Re-creating the cache...")]
    private partial void LogCorruptedCacheFile(string path);
}
*/
