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
        var figi = await dbContext.Instruments
            .Where(i => i.Name == "Роснефть")
            .Select(i => i.Figi)
            .FirstOrDefaultAsync(cancellation)
            ?? throw new Exception("Instrument not found");
        await tinkoffHistoryData.GetAsync(figi, DateTime.UtcNow.Year, cancellation);
    }
}
