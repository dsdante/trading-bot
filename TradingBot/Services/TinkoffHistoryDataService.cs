using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using TradingBot.Data;

namespace TradingBot;

public class TinkoffHistoryDataService(HttpClient httpClient, ILogger<TinkoffHistoryDataService> logger)
{
    /// <summary> Write candle history CSVs (in ASCII with LF newlines) to the specified stream </summary>
    public async Task DownloadCsvAsync(Stream destination, Instrument instrument, int year, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(destination, nameof(destination));
        if (!destination.CanWrite)
            throw new NotSupportedException("Stream does not support writing.");
        ArgumentNullException.ThrowIfNull(instrument, nameof(instrument));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(year, nameof(year));

        var url = $"https://invest-public-api.tinkoff.ru/history-data?figi={instrument.Figi}&year={year}";
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);  // TODO: HttpRequestException: No route to host
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var pipe = new Pipe();
        // Start filling the pipe in parallel.
        var writing = FillPipeAsync(stream, pipe.Writer, cancellation);

        using var resultOwner = MemoryPool<byte>.Shared.Rent(128);
        var result = resultOwner.Memory;
        // Populate the beginning of the result buffer with the instrument ID.
        var idLength = Encoding.ASCII.GetBytes(instrument.Id.ToString(), result.Span);
        var candleCount = 0;

        while (true)
        {
            var read = await pipe.Reader.ReadAsync(cancellation);
            var buffer = read.Buffer;
            while (true)
            {
                // Replace the GUID with the instrument ID and the trailing semicolon with a newline.

                var endOfLine = buffer.PositionOf((byte)'\n') ?? default;
                if (endOfLine.GetObject() == null)
                    break;

                // Skip the GUID and the trailing semicolon.
                var line = buffer.Slice(36, buffer.GetOffset(endOfLine) - buffer.GetOffset(buffer.Start) - 37);
                var resultLength = idLength + (int)line.Length + 1;
                if (result.Length < resultLength)
                    throw new InternalBufferOverflowException("CSV line too long: " +
                        Encoding.ASCII.GetString(buffer.Slice(0, buffer.GetOffset(endOfLine) - buffer.GetOffset(buffer.Start))));
                line.CopyTo(result[idLength..].Span);
                result.Span[resultLength - 1] = (byte)'\n';

                await destination.WriteAsync(result[..resultLength], cancellation);
                candleCount++;
                buffer = buffer.Slice(buffer.GetPosition(1, endOfLine));
            }
            pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
            if (read.IsCompleted)
                break;
        }

        await writing;
        logger.LogInformation("{count} candles downloaded for {instrument} ({year}) from {url}", candleCount, instrument.Name, year, url);
    }

    // Write all files from a zip file as a single stream of data
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
}
