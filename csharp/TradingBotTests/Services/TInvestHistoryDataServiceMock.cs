using System.Net;
using TradingBot;
using TradingBot.Data;

namespace TradingBotTests;

internal class TInvestHistoryDataServiceMock : ITInvestHistoryDataService
{
    public int HistoryYears { get; set; } = 10;
    public List<(Instrument instrument, int year)> Requests { get; } = [];
    public List<int> YearServerErrors { get; } = [];

    public Task<RateLimitResponse> DownloadCsvAsync(Instrument instrument, int year, CancellationToken _ = default)
    {
        Requests.Add((instrument, year));
        int lastYear = DateTime.UtcNow.Year;
        int firstYear = lastYear - HistoryYears + 1;

        HttpStatusCode status;
        if (YearServerErrors.Contains(year))
        {
            status = HttpStatusCode.GatewayTimeout;
            YearServerErrors.Remove(year);
        }
        else if (firstYear <= year && year <= lastYear)
        {
            status = HttpStatusCode.OK;
        }
        else
        {
            status = HttpStatusCode.NotFound;
        }

        return Task.FromResult(new RateLimitResponse(status, 1, default));
    }

    public void Reset()
    {
        Requests.Clear();
        YearServerErrors.Clear();
    }
}
