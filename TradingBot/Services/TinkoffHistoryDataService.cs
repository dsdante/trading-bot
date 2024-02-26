using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using TradingBot.Data;

namespace TradingBot;

public class TinkoffHistoryDataService(HttpClient httpClient)
{
    public async Task DownloadCsvAsync(Instrument instrument, int year, CancellationToken cancellation)
    {
        using var response = await httpClient.GetAsync(
            $"https://invest-public-api.tinkoff.ru/history-data?figi={instrument.Figi}&year={year}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellation);
        response.EnsureSuccessStatusCode();

        var pipe = new Pipe();
        var writing = ReadPipeAsync(pipe.Reader, instrument.Id, cancellation);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var archive = new ZipArchive(stream);
        foreach (var entry in archive.Entries)
        {
            await using var entryStream = entry.Open();
            await entryStream.CopyToAsync(pipe.Writer, cancellation);
        }

        await writing;
    }

    private async Task ReadPipeAsync(PipeReader reader, short insturmentId, CancellationToken cancellation)
    {
        var idText = Encoding.ASCII.GetBytes(insturmentId.ToString());
    }
}
