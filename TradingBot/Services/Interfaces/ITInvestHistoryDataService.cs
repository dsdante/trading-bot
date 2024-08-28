using TradingBot.Data;

namespace TradingBot;

public interface ITInvestHistoryDataService
{
    Task<RateLimitResponse> DownloadCsvAsync(Instrument instrument, int year, CancellationToken cancellation);
}
