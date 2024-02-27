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

    public async Task DownloadHistory(CancellationToken cancellation)
    {
        var instrument = await dbContext.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Name == "Роснефть", cancellation)
            ?? throw new Exception("Instrument not found");

        await using var stream = await CandleHistoryCsvStream.OpenAsync(dbContext.Database.GetConnectionString()!, cancellation);
        await tinkoffHistoryData.DownloadCsvAsync(stream, instrument, DateTime.UtcNow.Year, cancellation);
        await stream.CommitAsync(cancellation);
    }
}
