using System.Diagnostics;

namespace TradingBot;

public class Worker(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime) : BackgroundService
{
    // Main logic entry point
    private static async Task RunAsync(IServiceProvider services, CancellationToken cancellation)
    {
        var dbContext = services.GetRequiredService<Data.TradingBotDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellation);

        //var historyService = services.GetRequiredService<HistoryService>();
        //await historyService.UpdateInstrumentsAsync(cancellation);
        //await historyService.DownloadHistoryBeginningAsync(cancellation);
        //await historyService.UpdateHistoryAsync(cancellation);

        //var featureService = services.GetRequiredService<FeatureService>();
        //await featureService.UpdateFeaturesAsync(cancellation);

        //var machineLearningService = services.GetRequiredService<MachineLearningService>();
        //await machineLearningService.DumpFeatures(cancellation);
    }

    // Most services are scoped by default; this is a wrapper that creates a scope for them.
    // https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service
    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await RunAsync(scope.ServiceProvider, cancellation);
            lifetime.StopApplication();
        }
        catch (Exception ex) when (!Debugger.IsAttached || ex is OperationCanceledException)
        {
            // Re-throw the underlying OperationCanceledException.
            if (ex is OperationCanceledException)
                throw;

            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
                if (ex is OperationCanceledException)
                    throw ex;
            }

            throw;
        }
    }
}
