using TradingBot.Services;

namespace TradingBot;

public class Worker(TinkoffService tinkoff, ILogger<Worker> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    // Main top-level logic
    private async Task RunAsync(CancellationToken cancellation)
    {
        int count = 0;
        await foreach (var instrument in tinkoff.GetInstruments(cancellation))
        {
            count++;
            logger.LogInformation("{count} {type}: {instrument}", count, instrument.AssetType, instrument.Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        try
        {
            await RunAsync(cancellation);
            lifetime.StopApplication();
        }
        catch (Exception e)
        {
            // Re-throw the underlying OperationCanceledException.
            if (e is OperationCanceledException)
                throw;
            while (e.InnerException != null)
            {
                e = e.InnerException;
                if (e is OperationCanceledException cancelled)
                    throw cancelled;
            }
            throw;
        }
    }
}
