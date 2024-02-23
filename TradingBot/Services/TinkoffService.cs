using System.Runtime.CompilerServices;
using Tinkoff.InvestApi;
using TradingBot.Data;
using InstrumentsServiceClient = Tinkoff.InvestApi.V1.InstrumentsService.InstrumentsServiceClient;

namespace TradingBot.Services;

public class TinkoffService(InvestApiClient tinkoff)
{
    private static readonly Func<InstrumentsServiceClient, CancellationToken, Task<IEnumerable<Instrument>>>[] instrumentGetters =
    [
        async (instruments, ct) => (await instruments.BondsAsync(ct)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, ct) => (await instruments.CurrenciesAsync(ct)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, ct) => (await instruments.EtfsAsync(ct)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, ct) => (await instruments.FuturesAsync(ct)).Instruments.Select(i => i.ToInstrument()),
        async (instruments, ct) => (await instruments.SharesAsync(ct)).Instruments.Select(i => i.ToInstrument()),
    ];

    public async IAsyncEnumerable<Instrument> GetInstruments([EnumeratorCancellation]CancellationToken cancellation)
    {
        var tasks = instrumentGetters
            .Select(getter => getter(tinkoff.Instruments, cancellation))
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
