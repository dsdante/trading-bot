using TradingBot.Data;
using TradingBot.Services;

namespace TradingBot;

/// <summary> Top-level logic </summary>
public class Worker(
    TinkoffService tinkoff,
    IHostApplicationLifetime lifetime,
    IServiceScopeFactory scopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    // Entry point
    private async Task RunAsync(CancellationToken cancellation)
    {
        /*
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellation);
        */
        await UpdateInstruments(cancellation);
    }

    /// <summary> Refresh instrument info from Tinkoff API </summary>
    public async Task UpdateInstruments(CancellationToken cancellation)
    {
        logger.LogInformation("Updating the instruments...");

        var instruments = new List<Instrument>();
        await foreach (var instrument in tinkoff.GetInstruments(cancellation))
            instruments.Add(instrument);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        await dbContext.UpsertRangeAsync(instruments, cancellation);
        await dbContext.SaveChangesAsync(cancellation);
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
