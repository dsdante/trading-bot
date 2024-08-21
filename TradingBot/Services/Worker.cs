namespace TradingBot;

// Most services are scoped by default; this is a wrapper that creates a scope for them.
// https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service
public class Worker(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime) : BackgroundService
{
    // Main logic entry point
    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            /*
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.TradingBotDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellation);
            */

            var historyService = scope.ServiceProvider.GetRequiredService<HistoryService>();
            await historyService.UpdateInstruments(cancellation);
            //await historyService.DownloadHistory(cancellation);

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
