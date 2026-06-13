using Microsoft.Extensions.Options;
using Npgsql;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using TradingBot.Data;

namespace TradingBot;

public partial class TInvestHistoryDataService(
        HttpClient httpClient,
        IOptions<NpgsqlConnectionStringBuilder> connectionString,
        ILoggerFactory loggerFactory,
        ILogger<TInvestHistoryDataService> logger)
    : ITInvestHistoryDataService
{
    /// <summary> Download candle history and write it to the destination. </summary>
    /// <seealso cref="https://russianinvestments.github.io/investAPI/get_history"/>
    /// <returns>(T-Invest API throttling limit, limit reset timeout)</returns>
    public async Task<RateLimitResponse> DownloadCsvAsync(
        Instrument instrument,
        int year,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(instrument, nameof(instrument));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year, nameof(year));

        var stopwatch = Stopwatch.StartNew();
        var url = $"https://invest-public-api.tinkoff.ru/history-data?figi={instrument.Figi}&year={year}";

        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    LogDownloadFailed(instrument.AssetType, instrument.Name, year, response.StatusCode);
                else if (response.StatusCode is not HttpStatusCode.NotFound and not HttpStatusCode.InternalServerError)
                    LogDownloadFromUrlFailed(instrument.AssetType, instrument.Name, year, response.StatusCode, url);

                RateLimitResponse.TryGetFromResponse(response, out var failedRateLimitResponse);
                return failedRateLimitResponse;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellation);
            await using var destination = await CandleHistoryCsvStream.OpenAsync(
                connectionString.Value.ConnectionString,
                loggerFactory,
                cancellation);

            Pipe pipe = new();
            var fillPipeTask = FillPipeAsync(source, pipe.Writer, cancellation);
            var readRowCount = await ReadPipeAsync(pipe.Reader, destination, instrument.Id, cancellation);
            await fillPipeTask;

            int addedRowCount = await destination.CommitAsync(cancellation);
            if (addedRowCount == -1 || addedRowCount == readRowCount)
            {
                LogAllCandlesAdded(
                    instrument.AssetType,
                    instrument.Name,
                    year,
                    addedRowCount,
                    stopwatch.Elapsed);
            }
            else
            {
                LogSomeCandlesAdded(
                    instrument.AssetType,
                    instrument.Name,
                    year,
                    addedRowCount,
                    readRowCount,
                    stopwatch.Elapsed);
            }

            RateLimitResponse.TryGetFromResponse(response, out var rateLimitResponse);
            return rateLimitResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDownloadFailedWithException(instrument.AssetType, instrument.Name, year, url, ex);
            if (ex is TimeoutException)
                return new(HttpStatusCode.GatewayTimeout);
            return new((HttpStatusCode)520);
        }
    }

    // Read all files from a zip archive as a continuous stream.
    private static async Task FillPipeAsync(Stream source, PipeWriter destination, CancellationToken cancellation)
    {
        using ZipArchive archive = new(source);

        foreach (var entry in archive.Entries)
        {
            await using var stream = entry.Open();

            while (true)
            {
                var buffer = destination.GetMemory();
                var readCount = await stream.ReadAsync(buffer, cancellation);
                if (readCount == 0)
                    break;

                destination.Advance(readCount);
                await destination.FlushAsync(cancellation);
            }
        }

        await destination.CompleteAsync();
    }

    // Read a CSV stream, process it, and write it to the destination.
    // Returns read candle count.
    private static async Task<int> ReadPipeAsync(
        PipeReader source,
        Stream destination,
        short instrumentId,
        CancellationToken cancellation)
    {
        using var resultOwner = MemoryPool<byte>.Shared.Rent(128);
        var resultBuffer = resultOwner.Memory;

        // Populate the beginning of the result buffer with the instrument ID.
        // We need ASCII, but UTF-8 will do for an integer.
        var resultSpan = resultBuffer.Span;
        instrumentId.TryFormat(resultSpan, out int idLength);
        resultSpan[idLength++] = (byte)';';

        var candleCount = 0;

        while (true)
        {
            var readResult = await source.ReadAsync(cancellation);
            var readBuffer = readResult.Buffer;

            while (true)
            {
                var writtenLength = ProcessLine(ref readBuffer, resultBuffer.Span[idLength..]);
                if (writtenLength == 0)
                    break;
                await destination.WriteAsync(resultBuffer[..(idLength + writtenLength)], cancellation);
                candleCount++;
            }

            source.AdvanceTo(readBuffer.Start, readBuffer.End);
            if (readResult.IsCompleted)
                break;
        }

        return candleCount;
    }

    // Replace the timestamp with minute count, trim the trailing semicolon, advance the buffer.
    // Returns the length of the written data.
    private static int ProcessLine(ref ReadOnlySequence<byte> source, Span<byte> destination)
    {
        var endOfLine = source.PositionOf((byte)'\n') ?? default;
        if (endOfLine.GetObject() == null)
            return 0;

        // Write timespan in minutes
        Span<char> timestampSpan = stackalloc char[19];
        Encoding.ASCII.GetChars(source.Slice(37, 19), timestampSpan);
        var timestamp = DateTime.ParseExact(timestampSpan, "s", DateTimeFormatInfo.InvariantInfo);
        var minutes = Candle.ToMinutes(timestamp);
        minutes.TryFormat(destination, out int minutesLength);
        destination = destination[minutesLength..];

        // Trim the GUID, the timestamp and the trailing semicolon.
        var line = source.Slice(57, source.GetOffset(endOfLine) - source.GetOffset(source.Start) - 58);
        if (destination.Length < line.Length + 1)
            throw new InternalBufferOverflowException($"CSV line is too long: " +
                Encoding.ASCII.GetString(source.Slice(0, endOfLine)));

        line.CopyTo(destination);
        destination[(int)line.Length] = (byte)'\n';

        // Advance the buffer.
        source = source.Slice(source.GetPosition(1, endOfLine));
        return minutesLength + (int)line.Length + 1;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = @"{assetType} {instrument} ({year}): {addedCount}/{readCount} candles added in {time:s\\.fff}s")]
    private partial void LogSomeCandlesAdded(AssetType assetType, string instrument, int year, int addedCount, int readCount, TimeSpan time);

    [LoggerMessage(Level = LogLevel.Information, Message = @"{assetType} {instrument} ({year}): {count} candles added in {time:s\\.fff}s")]
    private partial void LogAllCandlesAdded(AssetType assetType, string instrument, int year, int count, TimeSpan time);

    [LoggerMessage(Level = LogLevel.Error, Message = "{assetType} {instrument} ({year}): failed to download with {status}.")]
    private partial void LogDownloadFailed(AssetType assetType, string instrument, int year, HttpStatusCode status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{assetType} {instrument} ({year}): failed to download with {status} from {url}")]
    private partial void LogDownloadFromUrlFailed(AssetType assetType, string instrument, int year, HttpStatusCode status, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "{assetType} {instrument} ({year}) failed to download history from {url}")]
    private partial void LogDownloadFailedWithException(AssetType assetType, string instrument, int year, string url, Exception ex);
}
