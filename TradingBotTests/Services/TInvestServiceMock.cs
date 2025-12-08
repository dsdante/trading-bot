using System.Runtime.CompilerServices;
using TradingBot;
using TradingBot.Data;

namespace TradingBotTests;

internal class TInvestServiceMock : ITInvestService
{
    public Instrument DefaultInstrument { get; private set; } = default!;

    public List<Instrument> Instruments { get; } = [];

    public TInvestServiceMock() => Reset();

    public async IAsyncEnumerable<Instrument> GetInstrumentsAsync(
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        foreach (var instrument in Instruments)
            yield return await ValueTask.FromResult(instrument);
    }

    public void Reset()
    {
        DefaultInstrument = new Instrument
        {
            Uid = new("81575098-df8a-45c4-82dc-1b64374dcfdb"),
            Figi = "BBG000BBJQV0",
            Name = "NVIDIA",
            AssetType = AssetType.Share,
            Lot = 1,
            Otc = false,
            ForQualInvestor = true,
            ApiTradeAvailable = true,
        };

        Instruments.Clear();
        Instruments.Add(DefaultInstrument);
    }
}
