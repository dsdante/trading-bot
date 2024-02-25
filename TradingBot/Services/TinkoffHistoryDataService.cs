namespace TradingBot;

public class TinkoffHistoryDataService(HttpClient httpClient)
{
    public async Task GetAsync(string figi, int year, CancellationToken cancellation)
    {
        var response = await httpClient.GetAsync(
            $"https://invest-public-api.tinkoff.ru/history-data?figi={figi}&year={year}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellation);
        var data = await response.Content.ReadAsByteArrayAsync(cancellation);
        // TODO continue
    }
}
