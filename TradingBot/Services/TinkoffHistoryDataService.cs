using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;

namespace TradingBot;

public class TinkoffHistoryDataService(
    HttpClient httpClient,
    TradingBotDbContext dbContext,
    ILoggerFactory loggerFactory,
    ILogger<TinkoffHistoryDataService> logger)
{
    /// <summary> Download candle history and write it to the destination. </summary>
    /// <returns>(Tinkoff API throttling limit, limit reset timeout)</returns>
    public async Task<(HttpStatusCode status, int limit, DateTimeOffset limitTimeout)> DownloadCsvAsync(
        Instrument instrument,
        int year,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(instrument, nameof(instrument));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year, nameof(year));

        var stopwatch = Stopwatch.StartNew();
        var url = $"https://invest-public-api.tinkoff.ru/history-data?figi={instrument.Figi}&year={year}";
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, default, default);
        var limit = response.Headers.Get<int>("x-ratelimit-remaining");
        var limitTimeout = DateTimeOffset.Now.AddSeconds(response.Headers.Get<double>("x-ratelimit-reset"));

        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var destination = await CandleHistoryCsvStream.OpenAsync(
            dbContext.Database.GetConnectionString()!,
            loggerFactory,
            cancellation);

        var pipe = new Pipe();
        var fillPipeTask = FillPipeAsync(source, pipe.Writer, cancellation);
        var candleCount = await ReadPipeAsync(pipe.Reader, destination, instrument.Id, cancellation);
        await fillPipeTask;

        await destination.CommitAsync(cancellation);
        logger.LogInformation("Downloaded {count} candles in {time:F3}s for {instrument} ({year}) from {url}",
            candleCount, stopwatch.Elapsed.TotalSeconds, instrument.Name, year, url);
        return (response.StatusCode, limit, limitTimeout);
    }

    // Read all files from a zip archive as a continuous stream.
    private static async Task FillPipeAsync(Stream source, PipeWriter destination, CancellationToken cancellation)
    {
        using var archive = new ZipArchive(source);
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
    private static async Task<int> ReadPipeAsync(
        PipeReader source,
        Stream destination,
        short instrumentId,
        CancellationToken cancellation)
    {
        using var resultOwner = MemoryPool<byte>.Shared.Rent(128);
        var resultBuffer = resultOwner.Memory;
        // Populate the beginning of the result buffer with the instrument ID.
        var idLength = Encoding.ASCII.GetBytes(instrumentId.ToString(), resultBuffer.Span);
        var candleCount = 0;

        while (true)
        {
            var readResult = await source.ReadAsync(cancellation);
            var readBuffer = readResult.Buffer;
            while (true)
            {
                var line = ProcessLine(ref readBuffer, resultBuffer, idLength);
                if (line.IsEmpty)
                    break;
                await destination.WriteAsync(line, cancellation);
                candleCount++;
            }
            source.AdvanceTo(readBuffer.Start, readBuffer.End);
            if (readResult.IsCompleted)
                break;
        }

        return candleCount;
    }

    // Replace the GUID with the ID, trim the trailing semicolon, and advance the buffer.
    private static Memory<byte> ProcessLine(
        ref ReadOnlySequence<byte> readBuffer,
        Memory<byte> resultBuffer,
        int idLength)
    {
        var endOfLine = readBuffer.PositionOf((byte)'\n') ?? default;
        if (endOfLine.GetObject() == null)
            return default;

        // Trim the GUID and the trailing semicolon.
        var line = readBuffer.Slice(36, readBuffer.GetOffset(endOfLine) - readBuffer.GetOffset(readBuffer.Start) - 37);
        var resultLength = idLength + (int)line.Length + 1;
        if (resultBuffer.Length < resultLength)
            throw new InternalBufferOverflowException($"CSV line longer than {resultBuffer.Length} characters: " +
                Encoding.ASCII.GetString(readBuffer.Slice(0, readBuffer.GetPosition(1, line.End))));
        line.CopyTo(resultBuffer[idLength..].Span);
        resultBuffer.Span[resultLength - 1] = (byte)'\n';

        // Advance the buffer.
        readBuffer = readBuffer.Slice(readBuffer.GetPosition(1, endOfLine));
        return resultBuffer[..resultLength];
    }
}
