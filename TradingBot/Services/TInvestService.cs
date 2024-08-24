using System.Runtime.CompilerServices;
using Tinkoff.InvestApi;
using TradingBot.Data;
using InstrumentsServiceClient = Tinkoff.InvestApi.V1.InstrumentsService.InstrumentsServiceClient;

namespace TradingBot;

public class TInvestService(InvestApiClient tInvest)
{
    private static readonly Func<InstrumentsServiceClient, CancellationToken, Task<IEnumerable<Instrument>>>[] instrumentGetters =
    [
        async (instruments, cancellation) => (await instruments.BondsAsync(cancellation)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, cancellation) => (await instruments.CurrenciesAsync(cancellation)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, cancellation) => (await instruments.EtfsAsync(cancellation)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, cancellation) => (await instruments.FuturesAsync(cancellation)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, cancellation) => (await instruments.SharesAsync(cancellation)).Instruments.Select(i => i.ToInstrument()),
    ];

    public async IAsyncEnumerable<Instrument> GetInstrumentsAsync([EnumeratorCancellation]CancellationToken cancellation)
    {
        var tasks = instrumentGetters
            .Select(getter => getter(tInvest.Instruments, cancellation))
            .ToList();

        while (tasks.Count > 0)
        {
            var task = await Task.WhenAny(tasks);
            foreach (var instrument in task.Result)
                yield return instrument;
            tasks.Remove(task);
        }
    }
}
