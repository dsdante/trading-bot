using TradingBot.Data;

namespace TradingBot;

public interface ITInvestService
{
    IAsyncEnumerable<Instrument> GetInstrumentsAsync(CancellationToken cancellation);
}
